using ApiGateway.Booking;
using ApiGateway.Booking.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq.Protected;
using System.Net;

namespace ApiGateway.UnitTests.Booking;

public class BookingSyncTriggerTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly BookingSyncTrigger _trigger;

    public BookingSyncTriggerTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted));

        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://booking-api.test.com")
        };

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("BookingClient"))
            .Returns(httpClient);

        var options = Options.Create(new XpBookingOptions { SyncIntervalMinutes = 5 });

        _trigger = new BookingSyncTrigger(
            httpClientFactory.Object,
            _memoryCache,
            options,
            NullLogger<BookingSyncTrigger>.Instance);
    }

    [Fact]
    public void ShouldTriggerSync_WhenFirstCall_ReturnsTrue()
    {
        // Act & Assert
        _trigger.ShouldTriggerSync(42).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerSync_WhenCalledTwiceWithinInterval_ReturnsFalseOnSecond()
    {
        // Act
        _trigger.ShouldTriggerSync(42);

        // Assert
        _trigger.ShouldTriggerSync(42).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerSync_WhenDifferentUsers_ReturnsTrueForBoth()
    {
        // Act & Assert
        _trigger.ShouldTriggerSync(42).Should().BeTrue();
        _trigger.ShouldTriggerSync(99).Should().BeTrue();
    }

    [Fact]
    public async Task TriggerSync_ShouldPostToBookingSyncEndpoint()
    {
        // Act
        _trigger.TriggerSync(42);
        await Task.Delay(100);

        // Assert
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Post &&
                r.RequestUri!.PathAndQuery.Contains("/api/booking/sync") &&
                r.Headers.GetValues("CurrentUser").First() == "42"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TriggerSync_WhenHttpCallFails_ShouldNotThrow()
    {
        // Arrange
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Booking API down"));

        // Act
        var act = () =>
        {
            _trigger.TriggerSync(42);
            return Task.CompletedTask;
        };

        // Assert
        await act.Should().NotThrowAsync();
        await Task.Delay(100); // let fire-and-forget complete without crash
    }
}
