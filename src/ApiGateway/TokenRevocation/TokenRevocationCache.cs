using Microsoft.Extensions.Caching.Memory;

namespace ApiGateway.TokenRevocation;

/// <summary>
/// Implementation of token revocation cache using IMemoryCache
/// This implementation uses in-memory cache with Sticky Sessions (Session Affinity) in Azure App Service.
///
/// IMPORTANT: The cache is NOT shared across instances. This works correctly because:
/// - Azure App Service is configured with Sticky Sessions (ARR Affinity)
/// - The same user is always routed to the same instance
/// - Token revocation state is maintained per-instance
///
/// NOTE: Do NOT disable Sticky Sessions if using this cache implementation.
/// </summary>
public class TokenRevocationCache : ITokenRevocationCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenRevocationCache> _logger;
    private const string CacheKeyPrefix = "revoked_jti:";

    public TokenRevocationCache(IMemoryCache cache, ILogger<TokenRevocationCache> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void AddRevokedToken(string jti, DateTime expiration)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            _logger.LogWarning("[TOKEN-REVOKE] Attempted to revoke token with null or empty jti/uti");
            return;
        }

        var now = DateTime.UtcNow;
        var ttl = expiration - now;

        // Only add to cache if expiration is in the future
        if (ttl.TotalSeconds <= 0)
        {
            _logger.LogWarning("[TOKEN-REVOKE] Token with jti/uti {Jti} is already expired, not adding to cache", jti);
            return;
        }

        var cacheKey = $"{CacheKeyPrefix}{jti}";
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ttl);

        _cache.Set(cacheKey, true, cacheEntryOptions);

        _logger.LogInformation("[TOKEN-REVOKE] Token with jti/uti {Jti} added to in-memory revocation cache with TTL {TtlMinutes} minutes",
            jti, ttl.TotalMinutes);
    }

    public bool IsTokenRevoked(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        var cacheKey = $"{CacheKeyPrefix}{jti}";
        return _cache.TryGetValue(cacheKey, out _);
    }
}
