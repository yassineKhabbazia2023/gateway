using System.Text.Json;
using System.Web;
using ApiGateway.Contact.Exceptions;
using ApiGateway.Contact.Models;
using ApiGateway.Exceptions;

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
            throw new GatewayException(StatusCodes.Status404NotFound, Errors.NotFoundContactCode, string.Format(Errors.NotFoundContactMessage, userEmail));
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

    public async Task<Models.Contact?> GetContactByIdAsync(int contactId)
    {
        var response = await httpClient.GetAsync($"contact/{contactId}");
        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(jsonString))
            {
                return null;
            }
            return JsonSerializer.Deserialize<Models.Contact>(jsonString);
        }
        return null;
    }

    private string ContactUrl(string userEmail)
    {
        string encodedEmail = HttpUtility.UrlEncode(userEmail);
        return $"contacts?Email={encodedEmail}";
    }
}
