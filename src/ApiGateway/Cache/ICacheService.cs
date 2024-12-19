namespace ApiGateway.Cache;

public interface ICacheService
{
    Task<Contact.Models.Contact?> GetAsync(string key);
    Task SetContactAsync(string userEmail, Contact.Models.Contact contact);
    T? GetOrCreate<T>(string cacheKey, T? data = default, TimeSpan? slidingExpiration = null);
}
