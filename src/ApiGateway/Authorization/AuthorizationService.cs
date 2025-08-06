using System.Text.Json;

namespace ApiGateway.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;

    public AuthorizationService(HttpClient httpClient)
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
                return [];
            }

            var contactResult = JsonSerializer.Deserialize<List<string>>(jsonString!);
            return contactResult ?? [];
        }

        return [];
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

    public async Task<List<string>> GetAllContactAuthorizationsAsync(int contactId)
    {
        var url = string.Concat($"api/authorizations?contactId={contactId}");
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(jsonString))
            {
                var contactResult = JsonSerializer.Deserialize<List<string>>(jsonString);
                if (contactResult != null)
                {
                    return contactResult;
                }
            }
        }

        return [];
    }
}
