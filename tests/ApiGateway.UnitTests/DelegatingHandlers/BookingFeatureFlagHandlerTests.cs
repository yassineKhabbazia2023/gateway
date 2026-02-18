using ApiGateway.Booking;
using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class BookingFeatureFlagHandlerTests
{
    private static (HttpMessageInvoker invoker, BookingFeatureFlagHandler handler) CreateHandler(
        Mock<IBookingExperienceGuards> bookingGuards, HttpResponseMessage? innerResponse = null)
    {
        var handler = new BookingFeatureFlagHandler(bookingGuards.Object, NullLogger<BookingFeatureFlagHandler>.Instance);

        if (innerResponse is not null)
        {
            var innerMock = new Mock<HttpMessageHandler>();
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(innerResponse);
            handler.InnerHandler = innerMock.Object;
        }

        return (new HttpMessageInvoker(handler), handler);
    }

    [Fact]
    public async Task Should_Return_403_When_FeatureFlag_Is_Disabled()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        var (invoker, _) = CreateHandler(bookingGuards);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Forbidden,
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingFeatureFlagDisabledCode, ErrorMessage = Errors.XpBookingFeatureFlagDisabledMessage })
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Forward_Request_When_FeatureFlag_Is_Enabled()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(bookingGuards, innerResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
