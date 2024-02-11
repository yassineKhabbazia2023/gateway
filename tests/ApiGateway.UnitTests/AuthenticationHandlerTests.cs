using System.Net;
using ApiGateway.DelegatingHandlers;
using ApiGateway.Security;

namespace ApiGateway.UnitTests;

public class AuthenticationHandlerTests
{
    private readonly Mock<IAuthenticationService> _mockAuthorizationService = new();

    [Fact]
    public async Task SendAsync_RequestIsAuthorized_ProceedsWithNextHandler()
    {
        // Arrange
        _mockAuthorizationService.Setup(x => x.IsAllowed(It.IsAny<HttpRequestMessage>())).Returns(true);
        var authenticationHandler = new AuthenticationHandler( _mockAuthorizationService.Object)
        {
            InnerHandler = new TestHandler(new HttpResponseMessage(HttpStatusCode.OK))
        };
        var invoker = new HttpMessageInvoker(authenticationHandler);

        // Act
        var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://pulse.com"), CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_RequestIsNotAuthorized_ReturnsUnauthorizedResponse()
    {
        // Arrange
        _mockAuthorizationService.Setup(x => x.IsAllowed(It.IsAny<HttpRequestMessage>())).Returns(false);
        var authenticationHandler = new AuthenticationHandler( _mockAuthorizationService.Object)
        {
            InnerHandler = new TestHandler(new HttpResponseMessage(HttpStatusCode.OK)) // InnerHandler will not be called
        };
        var invoker = new HttpMessageInvoker(authenticationHandler);

        // Act
        var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://pulse.com"), CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Unauthorized: Access is denied.");
    }

    private class TestHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;

        public TestHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}