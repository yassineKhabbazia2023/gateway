using ApiGateway.DelegatingHandlers;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApiGateway.Tests.DelegatingHandlers
{
    public class DownstreamExceptionHandlerTests
    {
        private readonly Mock<ILogger<DownstreamExceptionHandler>> _loggerMock;
        private readonly Fixture _fixture;
        private readonly Mock<HttpMessageHandler> _innerHandlerMock;

        public DownstreamExceptionHandlerTests()
        {
            _loggerMock = new Mock<ILogger<DownstreamExceptionHandler>>();
            _fixture = new Fixture();
            _innerHandlerMock = new Mock<HttpMessageHandler>();
        }

        [Fact]
        public async Task SendAsync_WhenResponseIsSuccessful_ShouldNotLogWarning()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");
            request.Headers.Add("X-Custom-Header", "TestValue");
            request.Content = new StringContent("test request content", Encoding.UTF8, "application/json");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test response content")
            };
            response.Headers.Add("X-Response-Header", "ResponseValue");

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(HttpStatusCode.OK);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task SendAsync_WhenResponseIs4xx_ShouldLogWarning(HttpStatusCode statusCode)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/api/resource");
            request.Headers.Add("Authorization", "Bearer token123");
            request.Content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");

            var responseContent = "{\"errorCode\":\"ERR001\",\"errorMessage\":\"Something went wrong\"}";
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            response.Headers.Add("X-Error", "ErrorDetails");

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(statusCode);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Downstream exception")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        public async Task SendAsync_WhenResponseIs5xx_ShouldLogError(HttpStatusCode statusCode)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/api/resource");
            request.Headers.Add("Authorization", "Bearer token123");
            request.Content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");

            var responseContent = "{\"errorCode\":\"ERR001\",\"errorMessage\":\"Something went wrong\"}";
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            response.Headers.Add("X-Error", "ErrorDetails");

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(statusCode);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Downstream exception")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenResponseIsNonJsonContent_ShouldLogWarningAndNotThrow()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");
            var htmlContent = "<html><body>502 Bad Gateway</body></html>";
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
            };

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(HttpStatusCode.BadGateway);

            // Verify deserialization failure was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to deserialize")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify error was still logged with raw content as fallback
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Downstream exception")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenInnerHandlerThrowsException_ShouldPropagateException()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");
            var expectedException = new HttpRequestException("Connection error");

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(expectedException);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => invoker.SendAsync(request, CancellationToken.None));

            exception.Should().BeSameAs(expectedException);
        }

        [Fact]
        public async Task SendAsync_ShouldForwardRequestToInnerHandler()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            HttpRequestMessage capturedRequest = null;

            _innerHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(response);

            var handler = new DownstreamExceptionHandler(_loggerMock.Object);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            capturedRequest.Should().NotBeNull();
            capturedRequest.RequestUri.Should().Be(request.RequestUri);
            capturedRequest.Method.Should().Be(request.Method);
        }
    }
}
