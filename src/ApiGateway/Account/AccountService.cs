using ApiGateway.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ApiGateway.Account;

[ExcludeFromCodeCoverage]
public class AccountService : IAccountService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public AccountService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<Paging<Models.Account>> GetContactRolesAsync(int contactId)
    {
        var toReturn = new Paging<Models.Account>();
        var url = $"api/roles?contactId={contactId}";
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            try
            {
                toReturn = JsonSerializer.Deserialize<Paging<Models.Account>>(stream, _jsonSerializerOptions) ?? toReturn;
            }
            catch (Exception)
            {
                return toReturn;
            }
        }

        return toReturn;
    }

    public async Task<bool> CheckContactRoleAsync(int contactId, int? accountId, string? accountNumber)
    {
        var accountParam = accountId.HasValue ? $"accountId={accountId}" : $"accountNumber={accountNumber}";

        var url = $"api/roles/check-contact-role-on-account?contactId={contactId}&{accountParam}";

        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            try
            {
                return JsonSerializer.Deserialize<bool>(stream, _jsonSerializerOptions);
            }
            catch (Exception)
            {
                return false;
            }
        }
        return false;
    }

    public async Task<bool> CheckContactsCommonAccountRole(int currentUserId, int contactId)
    {
        var url = $"api/roles/check?contactId={contactId}";

        // Transmet le CurrentUser dans le header de l'appel
        _httpClient.DefaultRequestHeaders.Add("CurrentUser", $"{currentUserId}");
        var response = await _httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            try
            {
                return JsonSerializer.Deserialize<bool>(stream, _jsonSerializerOptions);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return false;
    }

    public async Task<Models.Account?> GetAccountAsync(int accountId)
    {
        var url = $"api/accounts/{accountId}";
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<Models.Account>(stream, _jsonSerializerOptions);
        }
        throw new HttpRequestException($"GET {url} returned {response.StatusCode}");
    }

    public async Task<IReadOnlyCollection<FavoriteAccount>?> GetFavoriteAccountsByContactIdAsync(int contactId)
    {
        var url = $"api/favorites?contactId={contactId}";
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<IReadOnlyCollection<FavoriteAccount>>(stream, _jsonSerializerOptions) ?? [];
        }
        return [];
    }

    public async Task<Summary?> GetSummaryAsync(int accountId, int currentUserId)
    {
        var url = $"api/accounts/{accountId}/summary";
        _httpClient.DefaultRequestHeaders.Add("CurrentUser", $"{currentUserId}");
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Summary>();
        }

        return null;
    }
}
