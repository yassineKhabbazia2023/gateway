using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(HttpClient httpClient, ILogger<AuthorizationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public async Task<bool> CreateOrUpdateContactAccountAuthorizationAsync(int contactId, int accountId, IList<string> codes)
    {
        var url = $"/api/authorizations/configuration?contactId={contactId}&accountId={accountId}";
        try
        {
            _logger.LogInformation("Updating authorizations for ContactId {ContactId}, AccountId {AccountId} via {Url}", contactId, accountId, url);

            var response = await _httpClient.PostAsJsonAsync(url, codes);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Authorization update failed with status {StatusCode} for ContactId {ContactId}, AccountId {AccountId}. Response: {Response}",
                    response.StatusCode, contactId, accountId, errorContent);

                return false;
            }

            return true;
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Authorization update timed out for ContactId {ContactId}, AccountId {AccountId}", contactId, accountId);
            throw new HttpRequestException("Authorization API request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Authorization update request failed for ContactId {ContactId}, AccountId {AccountId}", contactId, accountId);
            throw;
        }
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
