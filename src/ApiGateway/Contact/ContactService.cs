using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Kpmg.Constellation.Security.Claims;
using ApiGateway.Contact.Models;

namespace ApiGateway.Contact;

public class ContactService : IContactService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ContactService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string?> GetContactAsync(string contactApiUri, string userEmail)
    {
        var httpClient = _httpClientFactory.CreateClient("contact_client");
        var response = await httpClient.GetAsync(ContactUrl(contactApiUri!, userEmail));
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(jsonString))
            {
                return null;
            }

            var contactResult = JsonSerializer.Deserialize<PagingResult>(jsonString);
            if (contactResult != null && contactResult.Items?.Count > 0)
            {
                return contactResult.Items[0].Id.ToString();
            }
        }

        return null;
    }

    private string ContactUrl(string contactApiUri, string userEmail) =>
        $"{contactApiUri}/contact/api/contacts?Type={GetUserType(userEmail)}&Email={userEmail}";

    private static string GetUserType(string userEmail) =>
        userEmail.EndsWith("@kpmg.fr", StringComparison.OrdinalIgnoreCase) ? "Collaborator" : "Customer";
}
