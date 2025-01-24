
using System.Text.Json;

namespace ApiGateway.Authorization;

public class AuthorizationSevice : IAuthorizationSevice
{
    private readonly HttpClient _httpClient;

    public AuthorizationSevice(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<string>> GetContactAuthorizationAsync(int contactId, int? accountId)
    {
        var url = string.Concat($"api/authorization?contactId={contactId}", accountId == null ? "" : $"&accountId={accountId}");
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(jsonString))
            {
                return new List<string>();
            }

            var contactResult = JsonSerializer.Deserialize<List<string>>(jsonString!);
            return contactResult ?? new List<string>();
        }

        return new List<string>();
    }

    public async Task<List<string>> GetAllContactAuthorizationAsync(int contactId, int? accountId)
    {
        var permissions = await GetContactAuthorizationAsync(contactId, null);

        if (accountId.HasValue)
        {
            var unitaryPermissions = await GetContactAuthorizationAsync(contactId, accountId);
            permissions.AddRange(unitaryPermissions);
        }

        return permissions;
    }
}
