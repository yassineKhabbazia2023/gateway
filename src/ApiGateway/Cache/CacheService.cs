using Microsoft.Extensions.Caching.Distributed;

namespace ApiGateway.Cache;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public CacheService(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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
}

