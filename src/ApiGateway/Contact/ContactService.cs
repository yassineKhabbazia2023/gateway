using System.Text.Json;
using System.Web;
using ApiGateway.Contact.Models;
using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.Contact;

public class ContactService(HttpClient httpClient) : IContactService
{
    private readonly HttpClient httpClient = httpClient;

    public async Task<Models.Contact?> GetContactAsync(string userEmail)
    {
        var uri = ContactUrl(userEmail);
        var response = await httpClient.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
        {
            // Fail-closed: a technical failure of the Contact service must not be treated
            // as a contact not found, otherwise callers (e.g. ContactHandler) would let
            // the request through without a validated identity.
            throw new GatewayException(StatusCodes.Status502BadGateway, Errors.ContactResolutionFailedCode, string.Format(Errors.ContactResolutionFailedMessage, (int)response.StatusCode));
        }

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
        var response = await httpClient.GetAsync($"api/contact/{contactId}");
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

    public async Task<ContactCreated> CreateContactForProspectAsync(CreateContactRequest request, string? contactEmail, CancellationToken ct)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/contacts")
        {
            Content = JsonContent.Create(request, options: jsonOptions)
        };
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            httpRequest.Headers.Add("ContactEmail", contactEmail);
        }

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ContactCreated>(jsonOptions, ct)
                      ?? throw new HttpRequestException("Contact creation response was empty.");
        return created;
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> CreateNewPasswordAsync(CreateNewPasswordRequest request, string? entityType, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(entityType)
            ? "api/authentication/createNewPassword"
            : $"api/authentication/createNewPassword?entityType={Uri.EscapeDataString(entityType)}";

        return await httpClient.PostAsJsonAsync(url, request, cancellationToken: ct);
    }

    private string ContactUrl(string userEmail)
    {
        string encodedEmail = HttpUtility.UrlEncode(userEmail);
        return $"api/contacts?Email={encodedEmail}";
    }

    public async Task<IEnumerable<int>> SendEmailAsync(int currentUserId, string accountNumber, int[] customerIDs, string? entityType = null)
    {
        var url = string.IsNullOrWhiteSpace(entityType)
            ? $"api/customers/bulk-invite/{accountNumber}"
            : $"api/customers/bulk-invite/{accountNumber}?entityType={Uri.EscapeDataString(entityType)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("CurrentUser", $"{currentUserId}");
        httpRequest.Content = JsonContent.Create(customerIDs);
        var response = await httpClient.SendAsync(httpRequest);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<IEnumerable<int>>(stream) ?? [];
        }

        return [];
    }
}
