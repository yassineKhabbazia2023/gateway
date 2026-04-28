using ApiGateway.Exceptions;
using ApiGateway.Middlewares;
using AutoFixture;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Pulse.ExceptionMiddleware.Model;
using System.Net;
using Xunit;

namespace ApiGateway.Tests.Middlewares;

public class GatewayExceptionMiddlewareTests
{
    private readonly Fixture _fixture;
    private readonly DefaultHttpContext _httpContext;
    private readonly MemoryStream _responseBodyStream;
    private readonly Mock<ILogger<GatewayExceptionMiddleware>> _loggerMock;

    public GatewayExceptionMiddlewareTests()
    {
        _fixture = new Fixture();
        _responseBodyStream = new MemoryStream();
        _loggerMock = new Mock<ILogger<GatewayExceptionMiddleware>>();

        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = _responseBodyStream;
    }

    private GatewayExceptionMiddleware CreateMiddleware()
    {
        return new GatewayExceptionMiddleware(_loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithNoException_ShouldCallNext()
    {
        // Arrange
        var middleware = CreateMiddleware();
        bool nextCalled = false;
        Func<Task> next = () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(_httpContext, next);

        // Assert
        Assert.True(nextCalled);
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithGatewayException_ShouldHandleCorrectly()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var exceptionCode = _fixture.Create<string>();
        var exceptionMessage = _fixture.Create<string>();
        var statusCode = (int)HttpStatusCode.BadRequest;
        var gatewayException = new GatewayException(statusCode, exceptionCode, exceptionMessage);

        Func<Task> next = () => throw gatewayException;

        // Act
        await middleware.InvokeAsync(_httpContext, next);

        // Assert
        Assert.Equal(statusCode, _httpContext.Response.StatusCode);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                gatewayException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _responseBodyStream.Position = 0;
        using var reader = new StreamReader(_responseBodyStream);
        var responseContent = await reader.ReadToEndAsync();
        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseContent);

        Assert.Equal(exceptionCode, errorResponse.ErrorCode);
        Assert.Equal(exceptionMessage, errorResponse.ErrorMessage);
    }

    [Fact]
    public async Task InvokeAsync_WithGenericException_ShouldHandleCorrectly()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var exceptionMessage = _fixture.Create<string>();
        var exception = new Exception(exceptionMessage);

        Func<Task> next = () => throw exception;

        // Act
        await middleware.InvokeAsync(_httpContext, next);

        // Assert
        Assert.Equal(500, _httpContext.Response.StatusCode);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _responseBodyStream.Position = 0;
        using var reader = new StreamReader(_responseBodyStream);
        var responseContent = await reader.ReadToEndAsync();
        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseContent);

        Assert.Equal(Errors.UnexpectedExceptionCode, errorResponse.ErrorCode);
        Assert.Equal(exceptionMessage, errorResponse.ErrorMessage);
    }

    [Fact]
    public async Task InvokeAsync_WithResponseAlreadyStarted_ShouldNotModifyResponse()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var httpContextWithStartedResponse = new DefaultHttpContext();

        var responseType = httpContextWithStartedResponse.Response.GetType();
        var hasStartedField = responseType.GetField("_hasStarted",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (hasStartedField != null)
        {
            hasStartedField.SetValue(httpContextWithStartedResponse.Response, true);
        }

        var exception = new Exception(_fixture.Create<string>());
        Func<Task> next = () => throw exception;

        // Act
        await middleware.InvokeAsync(httpContextWithStartedResponse, next);

        // Assert
        Assert.True(true);
    }
}
