using System.Net;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.FeatureFlags;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Mocks
{
    public class MockResponseHandlerTests
    {
        private readonly Mock<IMockResponseRepository> _mockRepo = new();
        private readonly Mock<IFeatureFlagService> _featureFlagService = new(MockBehavior.Strict);
        private readonly Mock<ILogger<MockResponseHandler>> _logger = new();

        [Fact]
        public async Task SendAsync_MocksFeatureDisabled_ReturnsFallbackWithoutCallingRepository()
        {
            // Arrange
            _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureFlagService.Object, _logger.Object)
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
            _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((false, null)!);

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureFlagService.Object, _logger.Object)
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
            _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var expectedContent = "{\"key\":\"value\"}";
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((true, expectedContent));

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test?param=value");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureFlagService.Object, _logger.Object)
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
            _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockRepo.Setup(repo => repo.GetJsonContentAsync(It.IsAny<string>()))
                .ReturnsAsync((false, null));

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/invalidpath");
            var handler = new MockResponseHandler(_mockRepo.Object, _featureFlagService.Object, _logger.Object)
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
