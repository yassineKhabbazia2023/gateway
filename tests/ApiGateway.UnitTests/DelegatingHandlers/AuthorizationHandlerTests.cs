using System.Net;
using ApiGateway.DelegatingHandlers;
using Microsoft.Extensions.Logging;
namespace ApiGateway.UnitTests.DelegatingHandlers;

public class AuthorizationHandlerTests
{
    private readonly Mock<ILogger<AuthorizationHandler>> _loggerMock;
    private readonly HttpClient _client;

    public AuthorizationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<AuthorizationHandler>>();
        
        var authorizationHandler = new AuthorizationHandler(_loggerMock.Object);
        var innerHandler = new TestHttpMessageHandler();
        authorizationHandler.InnerHandler = innerHandler;

        _client = new HttpClient(authorizationHandler)
        {
            BaseAddress = new Uri("http://test.com"),
        };
    }

    [Fact]
    public async Task SendAsync_WhenInvoked_LogsAuthorizationCheck()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Checking if the user is authorized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }
}
public class TestHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}