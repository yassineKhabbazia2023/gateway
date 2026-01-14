using ApiGateway.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;

namespace ApiGateway.UnitTests.Middlewares;

public class QueryStringTokenMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<QueryStringTokenMiddleware>> _loggerMock;
    private readonly QueryStringTokenMiddleware _sut;
    private readonly DefaultHttpContext _httpContext;

    public QueryStringTokenMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<QueryStringTokenMiddleware>>();
        _sut = new QueryStringTokenMiddleware(_nextMock.Object, _loggerMock.Object);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_WithAllowedPathAndValidBase64Token_ShouldAddAuthorizationHeader()
    {
        // Arrange
        var originalToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        var base64Token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalToken));
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString($"?token={base64Token}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers[HeaderNames.Authorization].ToString().Should().Be($"Bearer {originalToken}");
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithNonAllowedPath_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-token"));
        _httpContext.Request.Path = "/gtw/other/api/endpoint";
        _httpContext.Request.QueryString = new QueryString($"?token={token}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithExistingAuthorizationHeader_ShouldNotOverwrite()
    {
        // Arrange
        var existingToken = "Bearer existing-token";
        var newToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("new-token"));
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString($"?token={newToken}");
        _httpContext.Request.Headers[HeaderNames.Authorization] = existingToken;

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers[HeaderNames.Authorization].ToString().Should().Be(existingToken);
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithNoTokenInQueryString_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyToken_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString("?token=");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespaceToken_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString("?token=   ");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidBase64Token_ShouldNotAddAuthorizationHeaderAndLogWarning()
    {
        // Arrange
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString("?token=not-valid-base64!!!");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
        _nextMock.Verify(next => next(_httpContext), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid token format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/gtw/pennylane/api/pennylane/terms/content")]
    [InlineData("/GTW/PENNYLANE/API/PENNYLANE/TERMS/CONTENT")]
    [InlineData("/Gtw/Pennylane/Api/Pennylane/Terms/Content")]
    public async Task InvokeAsync_WithAllowedPathCaseInsensitive_ShouldAddAuthorizationHeader(string path)
    {
        // Arrange
        var originalToken = "test-token";
        var base64Token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalToken));
        _httpContext.Request.Path = path;
        _httpContext.Request.QueryString = new QueryString($"?token={base64Token}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers[HeaderNames.Authorization].ToString().Should().Be($"Bearer {originalToken}");
    }

    [Fact]
    public async Task InvokeAsync_WithBase64UrlSafeToken_ShouldDecodeCorrectly()
    {
        // Arrange - Base64 URL safe uses - and _ instead of + and /
        var originalToken = "token+with/special+chars";
        var base64UrlSafe = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalToken))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString($"?token={base64UrlSafe}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers[HeaderNames.Authorization].ToString().Should().Be($"Bearer {originalToken}");
    }

    [Fact]
    public async Task InvokeAsync_WithBase64TokenWithoutPadding_ShouldDecodeCorrectly()
    {
        // Arrange - Base64 without padding (= characters)
        var originalToken = "ab";  // Results in Base64 that needs padding
        var base64WithoutPadding = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalToken)).TrimEnd('=');
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString($"?token={base64WithoutPadding}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers[HeaderNames.Authorization].ToString().Should().Be($"Bearer {originalToken}");
    }

    [Theory]
    [InlineData("/gtw/account/api/accounts")]
    [InlineData("/gtw/offer/api/offers")]
    [InlineData("/gtw/authorization/api/authorization")]
    [InlineData("/health")]
    [InlineData("/swagger")]
    public async Task InvokeAsync_WithNonWhitelistedPaths_ShouldNotProcessToken(string path)
    {
        // Arrange
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-token"));
        _httpContext.Request.Path = path;
        _httpContext.Request.QueryString = new QueryString($"?token={token}");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ShouldAlwaysCallNext()
    {
        // Arrange - Even with invalid token, next should be called
        _httpContext.Request.Path = "/gtw/pennylane/api/pennylane/terms/content";
        _httpContext.Request.QueryString = new QueryString("?token=invalid!!!");

        // Act
        await _sut.InvokeAsync(_httpContext);

        // Assert
        _nextMock.Verify(next => next(_httpContext), Times.Once);
    }
}
