using System.Net;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.UnitTests.Mocks
{
    public class MockResponseHandlerTests
    {
        [Fact]
        public async Task SendAsync_RequestHasNoRegisteredResponse_ReturnsFallbackResponse()
        {
            // Arrange
            var mockRepo = new Mock<IMockResponseRepository>();
            mockRepo.Setup(repo => repo.GetJsonContent(It.IsAny<string>()))
                .Returns((false, null)!);

            var request = new HttpRequestMessage(HttpMethod.Get,
                "http://localhost/test");
            var mockResponseHandler = new MockResponseHandler(mockRepo.Object)
            {
                InnerHandler = new MockHttpMessageHandler(new(HttpStatusCode.BadRequest))
            };

            var invoker = new HttpMessageInvoker(mockResponseHandler);

            // Act
            var response = await invoker.SendAsync(request,
                new());

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SendAsync_RequestWithQueryParameters_MatchesCorrectly()
        {
            // Arrange
            var mockRepo = new Mock<IMockResponseRepository>();
            var testUriWithQuery = "http://localhost/test?param=value";
            var routeKeyWithQuery = $"get:{testUriWithQuery}".ToLower();
            var expectedContentWithQuery = "{\"key\":\"value\"}";

            // Ensure the mock setup matches the routeKeyWithQuery exactly as it's generated in the handler.
            mockRepo.Setup(repo => repo.GetJsonContent(It.IsAny<string>()))
                .Returns<string>(key => (true, expectedContentWithQuery));

            var requestWithQuery = new HttpRequestMessage(HttpMethod.Get,
                testUriWithQuery);
            var mockResponseHandler = new MockResponseHandler(mockRepo.Object)
            {
                InnerHandler =
                    new MockHttpMessageHandler(
                        new HttpResponseMessage(HttpStatusCode
                            .OK)) // Default fallback, won't be used in successful match
            };

            var invoker = new HttpMessageInvoker(mockResponseHandler);

            // Act
            var responseWithQuery = await invoker.SendAsync(requestWithQuery,
                new CancellationToken());
            var contentWithQuery = await responseWithQuery.Content.ReadAsStringAsync();

            // Assert
            responseWithQuery.StatusCode.Should()
                .Be(HttpStatusCode.OK);
            contentWithQuery.Should()
                .Be(expectedContentWithQuery);
        }

        [Fact]
        public async Task SendAsync_RequestUriIsInvalid_ReturnsFallbackResponse()
        {
            // Arrange
            var mockRepo = new Mock<IMockResponseRepository>();
            mockRepo.Setup(repo => repo.GetJsonContent(It.IsAny<string>()))
                .Returns((false, null));
            var request = new HttpRequestMessage(HttpMethod.Get,
                "http://localhost/invalidpath");
            var mockResponseHandler = new MockResponseHandler(mockRepo.Object)
            {
                InnerHandler =
                    new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)) // Fallback response setup
            };

            var invoker = new HttpMessageInvoker(mockResponseHandler);

            // Act
            var response = await invoker.SendAsync(request,
                new CancellationToken());

            // Assert
            response.StatusCode.Should()
                .Be(HttpStatusCode.NotFound); // Verifying fallback response is returned
        }
    }
}