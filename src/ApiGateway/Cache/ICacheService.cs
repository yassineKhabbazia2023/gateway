using Microsoft.Extensions.Caching.Distributed;

namespace ApiGateway.Cache;

public interface ICacheService
{
    Task<string> GetAsync(string key);
    Task SetContactIdAsync(string userEmail, string contactId);
}
