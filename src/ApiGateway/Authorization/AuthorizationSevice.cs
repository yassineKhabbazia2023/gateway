
using ApiGateway.Contact.Exceptions;
using ApiGateway.Contact.Models;
using System.Net.Http;
using System;
using System.Text.Json;

namespace ApiGateway.Authorization;

public class AuthorizationSevice : IAuthorizationSevice
{
    private readonly HttpClient _httpClient;

    public AuthorizationSevice(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IList<string>> GetContactAuthorizationAsync(int contactId, int? accountId)
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

            var contactResult = JsonSerializer.Deserialize<IList<string>>(jsonString!);
            return contactResult ?? new List<string>();
        }

        return new List<string>();
    }
}
