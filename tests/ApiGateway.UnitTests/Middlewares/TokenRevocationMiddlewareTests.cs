using ApiGateway.Middlewares;
using ApiGateway.TokenRevocation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace ApiGateway.UnitTests.Middlewares;

public class TokenRevocationMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<TokenRevocationMiddleware>> _loggerMock;
    private readonly Mock<ITokenRevocationCache> _revocationCacheMock;
    private readonly TokenRevocationMiddleware _sut;
    private readonly DefaultHttpContext _httpContext;

    public TokenRevocationMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<TokenRevocationMiddleware>>();
        _revocationCacheMock = new Mock<ITokenRevocationCache>();
        _sut = new TokenRevocationMiddleware(_nextMock.Object, _loggerMock.Object);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_WithNoAuthorizationHeader_ShouldCallNext()
    {
        // Arrange - no Authorization header

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(cache => cache.IsTokenRevoked(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyAuthorizationHeader_ShouldCallNext()
    {
        // Arrange
        _httpContext.Request.Headers.Authorization = string.Empty;

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithValidNonRevokedToken_ShouldCallNext()
    {
        // Arrange
        var jti = "valid-jti-123";
        var token = CreateJwtToken(jti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(jti)).Returns(false);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(jti), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithRevokedToken_ShouldReturn401()
    {
        // Arrange
        var jti = "revoked-jti-456";
        var token = CreateJwtToken(jti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _httpContext.Response.Body = new MemoryStream();
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(jti)).Returns(true);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _httpContext.Response.ContentType.Should().StartWith("application/json");
        _nextMock.Verify(next => next(_httpContext), Times.Never);

        // Verify response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var responseJson = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
        responseJson.Should().ContainKey("errorCode");
        responseJson.Should().ContainKey("errorMessage");
        responseJson!["errorCode"].Should().Be("GTW019");
        responseJson!["errorMessage"].Should().Be("Accès non autorisé. Veuillez vous reconnecter.");
    }

    [Theory]
    [InlineData("/gtw/authentication/api/login")]
    [InlineData("/gtw/authentication/api/logout")]
    [InlineData("/gtw/authentication/api/refreshToken")]
    [InlineData("/health")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task InvokeAsync_WithSkippedPaths_ShouldCallNextWithoutCheck(string path)
    {
        // Arrange
        _httpContext.Request.Path = path;
        var token = CreateJwtToken("any-jti", DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("/gtw/authentication/api/login", "?returnUrl=/home")]
    [InlineData("/gtw/authentication/api/logout", "?force=true")]
    [InlineData("/gtw/authentication/api/refreshToken", "?client=web")]
    public async Task InvokeAsync_WithSkippedPathsAndQueryStrings_ShouldCallNextWithoutCheck(string path, string queryString)
    {
        // Arrange - In ASP.NET Core, Path and QueryString are separate
        _httpContext.Request.Path = path;
        _httpContext.Request.QueryString = new QueryString(queryString);
        var token = CreateJwtToken("any-jti", DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("/gtw/authentication/api/login/bypass")]
    [InlineData("/gtw/authentication/api/logout/extra")]
    [InlineData("/authentication/login")]
    [InlineData("/api/authentication/login")]
    [InlineData("/fake-gtw/authentication/api/login")]
    public async Task InvokeAsync_WithBypassAttempts_ShouldCheckRevocation(string path)
    {
        // Arrange - These paths should NOT be skipped and revocation should be checked
        _httpContext.Request.Path = path;
        var jti = "test-jti-123";
        var token = CreateJwtToken(jti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(jti)).Returns(false);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(jti), Times.Once,
            $"Path '{path}' should have triggered revocation check but didn't");
    }

    [Fact]
    public async Task InvokeAsync_WithTokenWithoutJti_ShouldCallNext()
    {
        // Arrange
        var token = CreateJwtTokenWithoutJti();
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidToken_ShouldCallNext()
    {
        // Arrange
        _httpContext.Request.Headers.Authorization = "Bearer invalid-token-format";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithRevokedToken_ShouldLogWarning()
    {
        // Arrange
        var jti = "revoked-jti-789";
        var token = CreateJwtToken(jti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _httpContext.Response.Body = new MemoryStream();
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(jti)).Returns(true);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE] Revoked token detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullNext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new TokenRevocationMiddleware(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("next");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new TokenRevocationMiddleware(_nextMock.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private static string CreateJwtToken(string jti, DateTime expiration)
    {
        // Create a simple JWT token for testing
        // Format: header.payload.signature (we only need a readable payload for testing)
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"jti\":\"{jti}\",\"exp\":{new DateTimeOffset(expiration).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    private static string CreateJwtTokenWithoutJti()
    {
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"sub\":\"user123\",\"exp\":{new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    private static string CreateJwtTokenWithUti(string uti, DateTime expiration)
    {
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"uti\":\"{uti}\",\"exp\":{new DateTimeOffset(expiration).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    private static string CreateJwtTokenWithSubIat(string sub, long iat, DateTime expiration)
    {
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"sub\":\"{sub}\",\"iat\":{iat},\"exp\":{new DateTimeOffset(expiration).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    private static string CreateJwtTokenWithEmptyJti(DateTime expiration)
    {
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"jti\":\"\",\"exp\":{new DateTimeOffset(expiration).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    private static string CreateJwtTokenWithEmptyUti(DateTime expiration)
    {
        var header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"uti\":\"\",\"exp\":{new DateTimeOffset(expiration).ToUnixTimeSeconds()}}}");
        var signature = "fake-signature";
        return $"{header}.{payload}.{signature}";
    }

    [Fact]
    public async Task InvokeAsync_WithUtiClaim_ShouldCheckRevocationWithUti()
    {
        // Arrange
        var uti = "azure-ad-uti-123";
        var token = CreateJwtTokenWithUti(uti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(uti)).Returns(false);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(uti), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE] Using uti claim as token identifier")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithSubIatClaims_ShouldCheckRevocationWithSubIat()
    {
        // Arrange
        var sub = "gigya-user-456";
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expectedTokenId = $"{sub}:{iat}";
        var token = CreateJwtTokenWithSubIat(sub, iat, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(expectedTokenId)).Returns(false);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(expectedTokenId), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE] Using sub:iat as token identifier")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithRevokedUtiToken_ShouldReturn401()
    {
        // Arrange
        var uti = "revoked-uti-789";
        var token = CreateJwtTokenWithUti(uti, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _httpContext.Response.Body = new MemoryStream();
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(uti)).Returns(true);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextMock.Verify(next => next(_httpContext), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithRevokedSubIatToken_ShouldReturn401()
    {
        // Arrange
        var sub = "gigya-user-999";
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expectedTokenId = $"{sub}:{iat}";
        var token = CreateJwtTokenWithSubIat(sub, iat, DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";
        _httpContext.Response.Body = new MemoryStream();
        _revocationCacheMock.Setup(c => c.IsTokenRevoked(expectedTokenId)).Returns(true);

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextMock.Verify(next => next(_httpContext), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyJti_ShouldFallbackToSubIat()
    {
        // Arrange - token with empty jti should fallback to checking uti, then sub+iat
        var token = CreateJwtTokenWithEmptyJti(DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE] Token does not contain jti, uti, or sub+iat claims")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyUti_ShouldFallbackToSubIat()
    {
        // Arrange - token with empty uti should fallback to sub+iat
        var token = CreateJwtTokenWithEmptyUti(DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE] Token does not contain jti, uti, or sub+iat claims")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task InvokeAsync_WithHealthSubPaths_ShouldSkipRevocationCheck(string path)
    {
        // Arrange
        _httpContext.Request.Path = path;
        var token = CreateJwtToken("any-jti", DateTime.UtcNow.AddHours(1));
        _httpContext.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await _sut.InvokeAsync(_httpContext, _revocationCacheMock.Object);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _revocationCacheMock.Verify(c => c.IsTokenRevoked(It.IsAny<string>()), Times.Never);
    }
}
