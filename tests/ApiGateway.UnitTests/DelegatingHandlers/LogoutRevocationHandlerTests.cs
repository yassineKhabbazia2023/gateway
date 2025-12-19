using ApiGateway.DelegatingHandlers;
using ApiGateway.TokenRevocation;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class LogoutRevocationHandlerTests
{
    private readonly Mock<ITokenRevocationCache> _revocationCacheMock;
    private readonly Mock<ILogger<LogoutRevocationHandler>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly HttpClient _httpClient;

    public LogoutRevocationHandlerTests()
    {
        _revocationCacheMock = new Mock<ITokenRevocationCache>();
        _loggerMock = new Mock<ILogger<LogoutRevocationHandler>>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();

        var handler = new LogoutRevocationHandler(_revocationCacheMock.Object, _loggerMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };

        _httpClient = new HttpClient(handler);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulLogoutAndValidHeaders_ShouldAddTokenToCache()
    {
        // Arrange
        var jti = "test-jti-123";
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var expUnixTimestamp = expiration.ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", jti);
        response.Headers.Add("X-Revoked-Exp", expUnixTimestamp);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(jti, It.Is<DateTime>(dt => Math.Abs((dt - expiration.UtcDateTime).TotalSeconds) < 2)),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("Logout response received")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify internal headers are removed to prevent information disclosure
        result.Headers.Contains("X-Revoked-Jti").Should().BeFalse("X-Revoked-Jti header should be removed from response");
        result.Headers.Contains("X-Revoked-Exp").Should().BeFalse("X-Revoked-Exp header should be removed from response");
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulLogoutButMissingHeaders_ShouldLogDebug()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("does not contain revocation headers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulLogoutButInvalidExpiration_ShouldLogWarning()
    {
        // Arrange
        var jti = "test-jti-123";
        var invalidExp = "invalid-timestamp";

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", jti);
        response.Headers.Add("X-Revoked-Exp", invalidExp);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("Invalid revocation headers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulLogoutButEmptyJti_ShouldLogWarning()
    {
        // Arrange
        var expUnixTimestamp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", "   ");
        response.Headers.Add("X-Revoked-Exp", expUnixTimestamp);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("Invalid revocation headers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenCacheThrowsException_ShouldLogError()
    {
        // Arrange
        var jti = "test-jti-123";
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var expUnixTimestamp = expiration.ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", jti);
        response.Headers.Add("X-Revoked-Exp", expUnixTimestamp);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        _revocationCacheMock
            .Setup(cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Throws(new Exception("Cache error"));

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("Failed to add revoked token to cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Verify internal headers are removed even when cache fails
        result.Headers.Contains("X-Revoked-Jti").Should().BeFalse("X-Revoked-Jti header should be removed from response even on error");
        result.Headers.Contains("X-Revoked-Exp").Should().BeFalse("X-Revoked-Exp header should be removed from response even on error");
    }

    [Fact]
    public async Task SendAsync_WithFailedLogoutResponse_ShouldNotProcessRevocation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        response.Headers.Add("X-Revoked-Jti", "test-jti");
        response.Headers.Add("X-Revoked-Exp", "12345678");

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithNonLogoutRequest_ShouldNotProcessRevocation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/api/contacts");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", "test-jti");
        response.Headers.Add("X-Revoked-Exp", "12345678");

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithGetRequestToLogout_ShouldNotProcessRevocation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", "test-jti");
        response.Headers.Add("X-Revoked-Exp", "12345678");

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithLogoutPathInQueryString_ShouldProcessRevocation()
    {
        // Arrange
        var jti = "test-jti-123";
        var expiration = DateTimeOffset.UtcNow.AddHours(1);
        var expUnixTimestamp = expiration.ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout?param=value");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Revoked-Jti", jti);
        response.Headers.Add("X-Revoked-Exp", expUnixTimestamp);

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _revocationCacheMock.Verify(
            cache => cache.AddRevokedToken(jti, It.IsAny<DateTime>()),
            Times.Once);

        // Verify internal headers are removed to prevent information disclosure
        result.Headers.Contains("X-Revoked-Jti").Should().BeFalse("X-Revoked-Jti header should be removed from response");
        result.Headers.Contains("X-Revoked-Exp").Should().BeFalse("X-Revoked-Exp header should be removed from response");
    }

    [Fact]
    public void Constructor_WithNullCache_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LogoutRevocationHandler(null!, _loggerMock.Object));

        exception.ParamName.Should().Be("revocationCache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LogoutRevocationHandler(_revocationCacheMock.Object, null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task SendAsync_LogsResponseHeaders_WhenLogoutIsSuccessful()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/authentication/logout");
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.Add("X-Custom-Header", "custom-value");

        _innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("Logout response received")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
