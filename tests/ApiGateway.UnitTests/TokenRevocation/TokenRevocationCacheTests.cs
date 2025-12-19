using ApiGateway.TokenRevocation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiGateway.UnitTests.TokenRevocation;

public class TokenRevocationCacheTests
{
    private readonly Mock<ILogger<TokenRevocationCache>> _loggerMock;
    private readonly IMemoryCache _memoryCache;
    private readonly TokenRevocationCache _sut;

    public TokenRevocationCacheTests()
    {
        _loggerMock = new Mock<ILogger<TokenRevocationCache>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new TokenRevocationCache(_memoryCache, _loggerMock.Object);
    }

    [Fact]
    public void AddRevokedToken_WithValidJtiAndFutureExpiration_ShouldAddToCache()
    {
        // Arrange
        var jti = "test-jti-123";
        var expiration = DateTime.UtcNow.AddHours(1);

        // Act
        _sut.AddRevokedToken(jti, expiration);

        // Assert
        var isRevoked = _sut.IsTokenRevoked(jti);
        isRevoked.Should().BeTrue();
    }

    [Fact]
    public void AddRevokedToken_WithNullJti_ShouldNotAddToCache()
    {
        // Arrange
        var expiration = DateTime.UtcNow.AddHours(1);

        // Act
        _sut.AddRevokedToken(null!, expiration);

        // Assert - verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("null or empty jti")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void AddRevokedToken_WithEmptyJti_ShouldNotAddToCache()
    {
        // Arrange
        var jti = string.Empty;
        var expiration = DateTime.UtcNow.AddHours(1);

        // Act
        _sut.AddRevokedToken(jti, expiration);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("null or empty jti")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void AddRevokedToken_WithPastExpiration_ShouldNotAddToCache()
    {
        // Arrange
        var jti = "expired-jti-123";
        var expiration = DateTime.UtcNow.AddHours(-1); // Already expired

        // Act
        _sut.AddRevokedToken(jti, expiration);

        // Assert
        var isRevoked = _sut.IsTokenRevoked(jti);
        isRevoked.Should().BeFalse();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("already expired")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void IsTokenRevoked_WithNonExistentJti_ShouldReturnFalse()
    {
        // Arrange
        var jti = "non-existent-jti";

        // Act
        var result = _sut.IsTokenRevoked(jti);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenRevoked_WithNullJti_ShouldReturnFalse()
    {
        // Act
        var result = _sut.IsTokenRevoked(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenRevoked_WithEmptyJti_ShouldReturnFalse()
    {
        // Act
        var result = _sut.IsTokenRevoked(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AddRevokedToken_ShouldSetCorrectTTL()
    {
        // Arrange
        var jti = "ttl-test-jti";
        var expirationMinutes = 30;
        var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

        // Act
        _sut.AddRevokedToken(jti, expiration);

        // Assert
        _sut.IsTokenRevoked(jti).Should().BeTrue();

        // Verify log message contains TTL information
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[TOKEN-REVOKE]") && v.ToString()!.Contains("TTL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void AddRevokedToken_MultipleTokens_ShouldTrackAllSeparately()
    {
        // Arrange
        var jti1 = "jti-1";
        var jti2 = "jti-2";
        var jti3 = "jti-3";
        var expiration = DateTime.UtcNow.AddHours(1);

        // Act
        _sut.AddRevokedToken(jti1, expiration);
        _sut.AddRevokedToken(jti2, expiration);
        _sut.AddRevokedToken(jti3, expiration);

        // Assert
        _sut.IsTokenRevoked(jti1).Should().BeTrue();
        _sut.IsTokenRevoked(jti2).Should().BeTrue();
        _sut.IsTokenRevoked(jti3).Should().BeTrue();
        _sut.IsTokenRevoked("non-existent").Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullCache_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new TokenRevocationCache(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new TokenRevocationCache(_memoryCache, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
