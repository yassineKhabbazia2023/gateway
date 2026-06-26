using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

/// <inheritdoc />
public sealed class MandatePaymentPreferencesClient(HttpClient httpClient) : IMandatePaymentPreferencesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task<MandatePaymentPreferenceResponse?> GetAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(
            $"api/onboarding/{accountId}/payment-preferences",
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MandatePaymentPreferenceResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException($"Payment preference response for account {accountId} was empty.");
    }

    /// <inheritdoc />
    public async Task<bool> MarkSentToAkuiteoAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(
            $"api/onboarding/{accountId}/payment-preferences/mark-sent-to-akuiteo",
            content: null,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SaveSignedMandateDocumentIdAsync(int accountId, CancellationToken ct, string signedMandateDocumentId)
    {
        using var response = await httpClient.PostAsync(
            $"api/onboarding/{accountId}/payment-preferences/signed-mandate-document-id?signedMandateDocumentId={Uri.EscapeDataString(signedMandateDocumentId)}",
            content: null,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> GetSignedMandateDocumentIdAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(
            $"api/onboarding/{accountId}/payment-preferences/sepa/signed-mandate-document-id",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadStringResponseAsync(response, ct)
            ?? throw new HttpRequestException($"Signed mandate document identifier response for account {accountId} was empty.");
    }

    /// <inheritdoc />
    public async Task<bool> SetOtherAsync(int accountId, string contactEmail, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/onboarding/{accountId}/payment-preferences/other");
        request.Headers.Add("ContactEmail", contactEmail);

        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> SetSepaAsync(
        int accountId,
        MandateSepaPaymentPreferenceRequest request,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/onboarding/{accountId}/payment-preferences/sepa",
            request,
            JsonOptions,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var sepaResponse = await response.Content.ReadFromJsonAsync<SepaPaymentPreferenceResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException($"SEPA payment preference response for account {accountId} was empty.");

        return sepaResponse.SignatureUrl;
    }

    /// <inheritdoc />
    public async Task<bool> ResetAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/onboarding/{accountId}/payment-preferences",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Reads a downstream string response that may be returned either as JSON or as plain text.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The response string, or <see langword="null"/> when the response body is empty.</returns>
    private static async Task<string?> ReadStringResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmedContent = content.Trim();
        if (!trimmedContent.StartsWith('"'))
        {
            return trimmedContent;
        }

        return JsonSerializer.Deserialize<string>(trimmedContent, JsonOptions);
    }
}
