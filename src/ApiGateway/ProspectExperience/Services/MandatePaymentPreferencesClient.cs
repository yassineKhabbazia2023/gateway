using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Helpers;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

/// <inheritdoc />
public sealed class MandatePaymentPreferencesClient(
    HttpClient httpClient,
    ILogger<MandatePaymentPreferencesClient> logger) : IMandatePaymentPreferencesClient
{
    private const int MaximumValidationErrorLength = 512;
    private const string RedactedBankIdentifier = "[REDACTED]";
    private const string MissingValidationDetails = "No validation details returned.";

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
        return await response.Content.ReadFromJsonAsync<MandatePaymentPreferenceResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Payment preference response for account {accountId} was empty.");
    }

    /// <inheritdoc />
    public async Task<MandateBankDetailsExtractionResponse?> ExtractBankDetailsAsync(
        string iban,
        string bic,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/onboarding/bank-details/extract",
            new MandateBankDetailsExtractionRequest
            {
                Iban = iban,
                Bic = bic
            },
            ProspectExperienceJsonOptions.Default,
            ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validationError = await ReadSanitizedValidationErrorAsync(response, iban, bic, ct);
            logger.LogWarning(
                "Mandat bank-details extraction returned BadRequest ({StatusCode}). ValidationError: {ValidationError}",
                (int)response.StatusCode,
                validationError);
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MandateBankDetailsExtractionResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException("Bank-details extraction response was empty.");
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
            ProspectExperienceJsonOptions.Default,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var sepaResponse = await response.Content.ReadFromJsonAsync<SepaPaymentPreferenceResponse>(ProspectExperienceJsonOptions.Default, ct)
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

    /// <inheritdoc />
    public async Task<bool> CleanupAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(
            $"api/onboarding/{accountId}/cleanup",
            content: null,
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

        return JsonSerializer.Deserialize<string>(trimmedContent, ProspectExperienceJsonOptions.Default);
    }

    /// <summary>
    /// Reads a Mandat validation response while removing bank identifiers and bounding the logged value.
    /// </summary>
    /// <param name="response">The Mandat HTTP response.</param>
    /// <param name="iban">The IBAN value to redact.</param>
    /// <param name="bic">The BIC value to redact.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A single-line validation error safe for structured logging.</returns>
    private static async Task<string> ReadSanitizedValidationErrorAsync(
        HttpResponseMessage response,
        string iban,
        string bic,
        CancellationToken ct)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return MissingValidationDetails;
        }

        var sanitized = responseBody
            .Replace(iban, RedactedBankIdentifier, StringComparison.OrdinalIgnoreCase)
            .Replace(bic, RedactedBankIdentifier, StringComparison.OrdinalIgnoreCase);
        sanitized = string.Join(
            ' ',
            sanitized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return sanitized.Length <= MaximumValidationErrorLength
            ? sanitized
            : sanitized[..MaximumValidationErrorLength];
    }
}
