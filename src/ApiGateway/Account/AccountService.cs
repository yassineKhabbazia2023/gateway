using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Models;

namespace ApiGateway.Account;

[ExcludeFromCodeCoverage]
public class AccountService : IAccountService
{
    private readonly HttpClient _httpClient;

    public AccountService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<Paging<Models.Account>> GetContactRolesAsync(int contactId)
    {
        var toReturn = new Paging<Models.Account>();
        var url = $"api/roles/{contactId}";
        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            try
            {
                toReturn = JsonSerializer.Deserialize<Paging<Models.Account>>(stream) ?? toReturn;
            }
            catch (Exception)
            {
                return toReturn;
            }
        }

        return toReturn;
    }
}