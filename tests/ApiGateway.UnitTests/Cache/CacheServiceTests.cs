using ApiGateway.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiGateway.UnitTests.Cache;

public class CacheServiceTests
{
    private readonly IDistributedCache _mockDistributedCache;

    public CacheServiceTests()
    {
        var opt = Options.Create(new MemoryDistributedCacheOptions());
        _mockDistributedCache = new MemoryDistributedCache(opt);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExistsAndStillValid_ShouldReturnsValue()
    {
        // Arrange
        var key = "testKey";
        var expectedValue = "testValue";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        };
        await _mockDistributedCache.SetStringAsync(key, expectedValue, options);
        var cacheService = new CacheService(_mockDistributedCache);

        // Act
        var result = await cacheService.GetAsync(key);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExistsAndButNotValid_ShouldReturnsValue()
    {
        // Arrange
        var key = "testKey";
        var expectedValue = "testValue";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMicroseconds(1),
        };
        await _mockDistributedCache.SetStringAsync(key, expectedValue, options);
        var cacheService = new CacheService(_mockDistributedCache);
        
        // Simulates cache expiration 
        await Task.Delay(TimeSpan.FromMicroseconds(2));

        // Act
        var result = await cacheService.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ShouldReturnsNull()
    {
        // Arrange
        var key = "nonExistingKey";
        var cacheService = new CacheService(_mockDistributedCache);

        // Act
        var result = await cacheService.GetAsync(key);

        // Assert
        result.Should().BeNull();

    }

    [Fact]
    public async Task SetContactIdAsync_WhenKeyNotExist_ShouldInsertConatcInCache()
    {
        // Arrange
        var key = "testKey";
        var expectedValue = "testValue";
        var cacheService = new CacheService(_mockDistributedCache);

        // Act
        await cacheService.SetContactIdAsync(key, expectedValue);

        // Assert
        var result = await cacheService.GetAsync(key);
        result.Should().Be(expectedValue);
    }
}

