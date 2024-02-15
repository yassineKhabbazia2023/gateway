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

        [Fact]
        public async Task SendAsync_RequestHasRegisteredResponse_ReturnsMockResponse()
        {
            // Arrange
            var mockFilePath = "./mocks/testResponse.json";
            var directoryPath = Path.GetDirectoryName(mockFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath!); // Ensure the directory exists
            }

            var mockResponseContent = "{\"key\":\"value\"}";
            File.WriteAllText(mockFilePath, mockResponseContent);

            var mockRepo = new Mock<IMockResponseRepository>();
            mockRepo.Setup(repo => repo.GetJsonContent(It.IsAny<string>()))
                .Returns((true, mockFilePath));

            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            var mockResponseHandler = new MockResponseHandler(mockRepo.Object);

            var invoker = new HttpMessageInvoker(mockResponseHandler);

            // Act
            var response = await invoker.SendAsync(request, new());

            // Cleanup
            if (File.Exists(mockFilePath))
            {
                File.Delete(mockFilePath); // Cleanup the file after test
            }
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true); // Optionally, cleanup the directory after test if needed
            }

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody.Should().Be(mockResponseContent);
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
