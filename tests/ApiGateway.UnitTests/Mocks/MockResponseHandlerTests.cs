using System.Net;
using ApiGateway.DelegatingHandlers.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace ApiGateway.UnitTests.Mocks
{
    public class MockResponseHandlerTests
    {
        private readonly Mock<IMockResponseRepository> _mockRepo = new();
        private readonly Mock<IFeatureManager> _featureManager = new(MockBehavior.Strict);
        private readonly Mock<ILogger<MockResponseHandler>> _logger = new();

        [Fact]
        public async Task SendAsync_MocksFeatureDisabled_ReturnsFallbackWithoutCallingRepository()
        {
            // Arrange
            _featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(false);

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureManager.Object, _logger.Object)
            {
                InnerHandler = new MockHttpMessageHandler(new(HttpStatusCode.BadRequest))
            };
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var response = await invoker.SendAsync(request, new());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            _mockRepo.Verify(r => r.GetJsonContentAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_MocksEnabled_NoRegisteredResponse_ReturnsFallbackResponse()
        {
            // Arrange
            _featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((false, null)!);

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureManager.Object, _logger.Object)
            {
                InnerHandler = new MockHttpMessageHandler(new(HttpStatusCode.BadRequest))
            };
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var response = await invoker.SendAsync(request, new());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendAsync_MocksEnabled_RequestWithQueryParameters_MatchesCorrectly()
        {
            // Arrange
            _featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);
            var expectedContent = "{\"key\":\"value\"}";
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((true, expectedContent));

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test?param=value");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureManager.Object, _logger.Object)
            {
                InnerHandler = new MockHttpMessageHandler(new(HttpStatusCode.OK))
            };
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var response = await invoker.SendAsync(request, new CancellationToken());
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Be(expectedContent);
        }

        [Fact]
        public async Task SendAsync_MocksEnabled_RequestUriIsInvalid_ReturnsFallbackResponse()
        {
            // Arrange
            _featureManager.Setup(f => f.IsEnabledAsync("Mocks")).ReturnsAsync(true);
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((false, null));

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/invalidpath");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureManager.Object, _logger.Object)
            {
                InnerHandler = new MockHttpMessageHandler(new(HttpStatusCode.NotFound))
            };
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var response = await invoker.SendAsync(request, new CancellationToken());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
