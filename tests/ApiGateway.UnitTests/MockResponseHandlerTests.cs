using System.Net;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.UnitTests
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

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var mockResponseHandler = new MockResponseHandler(mockRepo.Object)
            {
                InnerHandler = new TestHandler(new(HttpStatusCode.BadRequest))
            };

            var invoker = new HttpMessageInvoker(mockResponseHandler);

            // Act
            var response = await invoker.SendAsync(request, new());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
}
