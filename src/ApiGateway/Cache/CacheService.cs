using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using YamlDotNet.Core.Tokens;

namespace ApiGateway.Cache;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IMemoryCache _memoryCache;


    public CacheService(IDistributedCache cache, IMemoryCache memoryCache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public async Task<Contact.Models.Contact?> GetAsync(string key)
    {
        var contactStr = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(contactStr))
        {
            return null;
        }
        return System.Text.Json.JsonSerializer.Deserialize<Contact.Models.Contact>(contactStr);
    }

    public async Task SetContactAsync(string userEmail, Contact.Models.Contact contact)
    {
        await _cache.SetStringAsync(userEmail, System.Text.Json.JsonSerializer.Serialize(contact), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = new TimeSpan(2, 0, 0)
        });
    }

    public T? GetOrCreate<T>(
        string cacheKey,
       T? data,
        TimeSpan? slidingExpiration = null)
    {
        if (EqualityComparer<T>.Default.Equals(data, default(T)))
        {
            _memoryCache.TryGetValue(cacheKey, out T? cachedData);
            return cachedData;
        }
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromSeconds(120)
        };

        _memoryCache.Set(cacheKey, data, cacheEntryOptions);
        return data;
    }
}

