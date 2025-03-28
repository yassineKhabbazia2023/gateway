using ApiGateway.DelegatingHandlers;
using AutoFixture;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
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
        private readonly TelemetryClient _telemetryClient;
        private readonly StubTelemetryChannel _telemetryChannel;
        private readonly Fixture _fixture;
        private readonly Mock<HttpMessageHandler> _innerHandlerMock;

        public DownstreamExceptionHandlerTests()
        {
            _loggerMock = new Mock<ILogger<DownstreamExceptionHandler>>();
            _fixture = new Fixture();
            _innerHandlerMock = new Mock<HttpMessageHandler>();

            // Create TelemetryClient with test channel
            _telemetryChannel = new StubTelemetryChannel();
            var configuration = new TelemetryConfiguration
            {
                TelemetryChannel = _telemetryChannel,
                InstrumentationKey = Guid.NewGuid().ToString()
            };
            _telemetryClient = new TelemetryClient(configuration);
        }

        [Fact]
        public async Task SendAsync_WhenResponseIsSuccessful_ShouldNotLogError()
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

            var handler = new DownstreamExceptionHandler(_loggerMock.Object, _telemetryClient);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(HttpStatusCode.OK);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);

            _telemetryChannel.SentTelemetry.Should().BeEmpty();
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task SendAsync_WhenResponseIsNotSuccessful_ShouldLogErrorAndTrackEvent(HttpStatusCode statusCode)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/api/resource");
            request.Headers.Add("Authorization", "Bearer token123");
            request.Content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");

            // Create response with proper error format for JSON deserialization
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

            var handler = new DownstreamExceptionHandler(_loggerMock.Object, _telemetryClient);
            handler.InnerHandler = _innerHandlerMock.Object;
            var invoker = new HttpMessageInvoker(handler);

            // Act
            var result = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(statusCode);

            // Verify logger was called with error level
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Response Status")), 
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            // Verify telemetry was sent using the stub channel
            _telemetryChannel.SentTelemetry.Should().NotBeEmpty();
            var eventTelemetry = _telemetryChannel.SentTelemetry.FirstOrDefault(t => t.GetType().Name == "EventTelemetry");
            eventTelemetry.Should().NotBeNull();

            // Additional verification on the event telemetry properties
            if (eventTelemetry is Microsoft.ApplicationInsights.DataContracts.EventTelemetry et)
            {
                et.Name.Should().Be("Downstream Exceptions");
                et.Properties.Should().ContainKey("Downstream Url");
                et.Properties.Should().ContainKey("Response Status");
                et.Properties.Should().ContainKey("ErrorCode");
                et.Properties.Should().ContainKey("ErrorMessage");
            }
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

            var handler = new DownstreamExceptionHandler(_loggerMock.Object, _telemetryClient);
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

            var handler = new DownstreamExceptionHandler(_loggerMock.Object, _telemetryClient);
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

    // Custom stub telemetry channel for testing
    public class StubTelemetryChannel : ITelemetryChannel
    {
        public List<ITelemetry> SentTelemetry { get; } = new List<ITelemetry>();
        public bool IsFlushed { get; private set; }
        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; }

        public void Send(ITelemetry item)
        {
            SentTelemetry.Add(item);
        }

        public void Flush()
        {
            IsFlushed = true;
        }

        public void Dispose()
        {
        }
    }
}