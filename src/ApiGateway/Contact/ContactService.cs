using System.Text.Json;
using ApiGateway.Contact.Exceptions;
using ApiGateway.Contact.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiGateway.Contact;

public class ContactService : IContactService
{
    private readonly HttpClient httpClient;

    public ContactService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<Models.Contact?> GetContactAsync(string userEmail)
    {
        var uri = ContactUrl(userEmail);
        var response = await httpClient.GetAsync(uri);
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
                return contactResult.Items[0];
            }

            throw new ContactNotFoundException();
        }

        return null;
    }

    public async Task<string?> GetContactIdAsync(string userEmail)
    {
        var response = await httpClient.GetAsync(ContactUrl(userEmail));
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

    private string ContactUrl(string userEmail) =>
        $"contacts?Type={GetUserType(userEmail)}&Email={userEmail}";

    private static string GetUserType(string userEmail) =>
        userEmail.EndsWith("@kpmg.fr", StringComparison.OrdinalIgnoreCase) ? "Collaborator" : "Customer";
}
