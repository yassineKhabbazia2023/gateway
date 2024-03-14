using Microsoft.Extensions.Caching.Distributed;

namespace ApiGateway.Cache;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public CacheService(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<string> GetAsync(string key)
    {
        return await _cache.GetStringAsync(key);
    }

    public async Task SetContactIdAsync(string userEmail, string contactId)
    {
        var now = DateTime.UtcNow;
        var targetTimeToday = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);
        if (now > targetTimeToday)
        {
            targetTimeToday = targetTimeToday.AddDays(1);
        }

        var expiration = targetTimeToday - now;

        await _cache.SetStringAsync(userEmail, contactId, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        });
    }
}

