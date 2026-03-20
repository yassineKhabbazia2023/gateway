using ApiGateway.Booking;
using ApiGateway.DelegatingHandlers;
using Moq.Protected;
using System.Net;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class BookingSyncHandlerTests
{
    private readonly Mock<IBookingSyncTrigger> _syncTriggerMock;

    public BookingSyncHandlerTests()
    {
        _syncTriggerMock = new Mock<IBookingSyncTrigger>();
    }

    private (HttpMessageInvoker invoker, BookingSyncHandler handler) CreateHandler(
        HttpResponseMessage? innerResponse = null)
    {
        var handler = new BookingSyncHandler(_syncTriggerMock.Object);

        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(innerResponse ?? new HttpResponseMessage(HttpStatusCode.OK));

        handler.InnerHandler = innerMock.Object;
        return (new HttpMessageInvoker(handler), handler);
    }

    [Fact]
    public async Task SendAsync_WhenCurrentUserHeader_AndShouldSync_CallsTriggerSync()
    {
        // Arrange
        _syncTriggerMock.Setup(s => s.ShouldTriggerSync(42)).Returns(true);
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/gtw/some-endpoint");
        request.Headers.Add("CurrentUser", "42");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _syncTriggerMock.Verify(s => s.TriggerSync(42), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenNoCurrentUserHeader_DoesNotCallSyncTrigger()
    {
        // Arrange
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/gtw/anonymous");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _syncTriggerMock.Verify(s => s.ShouldTriggerSync(It.IsAny<int>()), Times.Never);
        _syncTriggerMock.Verify(s => s.TriggerSync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenInvalidHeader_DoesNotCallSyncTrigger()
    {
        // Arrange
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/gtw/endpoint");
        request.Headers.Add("CurrentUser", "not-a-number");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _syncTriggerMock.Verify(s => s.ShouldTriggerSync(It.IsAny<int>()), Times.Never);
        _syncTriggerMock.Verify(s => s.TriggerSync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_AlwaysForwardsMainRequest()
    {
        // Arrange
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(expectedResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/gtw/endpoint");
        request.Headers.Add("CurrentUser", "42");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task SendAsync_WhenShouldTriggerSyncReturnsFalse_DoesNotCallTriggerSync()
    {
        // Arrange
        _syncTriggerMock.Setup(s => s.ShouldTriggerSync(42)).Returns(false);
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/gtw/endpoint");
        request.Headers.Add("CurrentUser", "42");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _syncTriggerMock.Verify(s => s.ShouldTriggerSync(42), Times.Once);
        _syncTriggerMock.Verify(s => s.TriggerSync(It.IsAny<int>()), Times.Never);
    }
}
