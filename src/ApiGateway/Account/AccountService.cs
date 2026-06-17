using ApiGateway.Models;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Services;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiGateway.Account;

[ExcludeFromCodeCoverage]
public class AccountService : IAccountService
{
    private readonly HttpClient _httpClient;
    private readonly IProspectApiClient _prospectApiClient;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public AccountService(HttpClient httpClient, IProspectApiClient prospectApiClient)
    {
        _httpClient = httpClient;
        _prospectApiClient = prospectApiClient;
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
            var summary = await response.Content.ReadFromJsonAsync<Summary>();

            if (summary?.AccountType == AccountType.PROSPECT.ToString())
            {
                summary.ProspectId = await _prospectApiClient.GetProspectIdByAccountIdAsync(accountId, CancellationToken.None);
            }

            return summary;
        }

        return null;
    }

    public async Task<AccountCreated> CreateAccountForProspectAsync(CreateAccountRequest request, int? currentUserId, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/accounts")
        {
            Content = JsonContent.Create(request, options: _jsonSerializerOptions)
        };
        if (currentUserId.HasValue)
        {
            httpRequest.Headers.Add("CurrentUser", currentUserId.Value.ToString());
        }

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AccountCreated>(_jsonSerializerOptions, ct)
                      ?? throw new HttpRequestException("Account creation response was empty.");
        return created;
    }

    public async Task<CreateRolesBulkResult> CreateRolesAsync(int accountId, IReadOnlyCollection<CreateRolesBulkItem> contacts, int? currentUserId, CancellationToken ct)
    {
        var payload = new CreateRolesBulkRequest { Contacts = contacts };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/roles/bulk?accountId={accountId}")
        {
            Content = JsonContent.Create(payload, options: _jsonSerializerOptions)
        };
        if (currentUserId.HasValue)
        {
            httpRequest.Headers.Add("CurrentUser", currentUserId.Value.ToString());
        }

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateRolesBulkResult>(_jsonSerializerOptions, ct)
                     ?? throw new HttpRequestException("Bulk roles creation response was empty.");
        return result;
    }

    /// <inheritdoc />
    public async Task<ProspectOnlyContactResult> GetProspectOnlyContactResultAsync(int contactId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"api/accounts/contacts/{contactId}/is-prospect-only", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return ProspectOnlyContactResult.NotFound;
        }

        response.EnsureSuccessStatusCode();
        var isProspectOnly = await response.Content.ReadFromJsonAsync<bool>(_jsonSerializerOptions, ct);
        return isProspectOnly ? ProspectOnlyContactResult.ProspectOnly : ProspectOnlyContactResult.NotProspectOnly;
    }
}
