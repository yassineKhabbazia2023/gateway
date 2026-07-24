using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Constants;
using ApiGateway.ProspectExperience.Helpers;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.ProspectExperience.Services;

public class ProspectApiClient(HttpClient httpClient, ILogger<ProspectApiClient> logger) : IProspectApiClient
{
    /// <inheritdoc />
    public async Task<InpiCompanyInfo> GetInpiCompanyInfoAsync(string siret, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/accounts/external-companies/{Uri.EscapeDataString(siret)}", ct);
        response.EnsureSuccessStatusCode();
        var company = await response.Content.ReadFromJsonAsync<ExternalCompanyResponse>(ProspectExperienceJsonOptions.Default, ct)
                      ?? throw new HttpRequestException($"External company response for SIRET {siret} was empty.");
        var address = company.Address ?? new ExternalCompanyAddressResponse();

        return new InpiCompanyInfo
        {
            LegalName = company.DisplayName,
            Siret = company.Siret,
            Siren = company.Siren,
            ZipCode = address.PostalCode,
            Address = address.Line1,
            City = address.City,
            NafCode = company.ApeCode,
            RegionCode = address.RegionCode
        };
    }

    /// <inheritdoc />
    public async Task<IncompleteProspectCreationState?> GetIncompleteProspectBySiretAsync(string siret, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/prospects/incomplete-by-siret/{Uri.EscapeDataString(siret)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IncompleteProspectCreationState>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Incomplete prospect state response for SIRET {siret} was empty.");
    }

    /// <inheritdoc />
    public async Task<DocumentsToUploadToExternalServiceResponse?> GetDocumentsToUploadToExternalServiceAsync(
        int prospectId,
        string stepName,
        CancellationToken ct)
    {
        var response = await httpClient.GetAsync(
            $"api/prospects/{prospectId}/documents/to-upload?stepName={Uri.EscapeDataString(stepName)}",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentsToUploadToExternalServiceResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Document upload plan response for prospect {prospectId} and step {stepName} was empty.");
    }

    /// <inheritdoc />
    public async Task<ProspectDocumentContentResponse?> GetDocumentAsync(int prospectId, int documentId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(
            $"api/prospects/{prospectId}/documents/{documentId}",
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var fileName = ExtractFileName(response);

        return new ProspectDocumentContentResponse(content, contentType, fileName);
    }

    /// <inheritdoc />
    public async Task<int?> UploadDocumentAsync(
        int prospectId,
        int currentUserId,
        string? contactEmail,
        string documentType,
        IFormFile file,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(documentType), "documentType");
        await using var stream = file.OpenReadStream();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", Path.GetFileName(file.FileName));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/prospects/{prospectId}/documents")
        {
            Content = content
        };
        request.Headers.Add("CurrentUser", currentUserId.ToString());
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            request.Headers.Add("ContactEmail", contactEmail);
        }

        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var upload = await response.Content.ReadFromJsonAsync<ProspectDocumentUploadResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Document upload response for prospect {prospectId} was empty.");

        return upload.DocumentId;
    }

    /// <inheritdoc />
    public async Task<DocumentUploadResultResponse?> RegisterDocumentUploadResultAsync(
        int prospectId,
        DocumentUploadResultRequest request,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/prospects/{prospectId}/documents/upload-result",
            request,
            ProspectExperienceJsonOptions.Default,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentUploadResultResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Document upload result response for prospect {prospectId} was empty.");
    }

    /// <inheritdoc />
    public async Task<ProspectAccountResponse?> GetProspectAccountAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"api/prospects/{prospectId}/with-signatory", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProspectAccountResponse>(ProspectExperienceJsonOptions.Default, ct)
            ?? throw new HttpRequestException($"Prospect account response for prospect {prospectId} was empty.");
    }

    /// <inheritdoc />
    public async Task<bool> IsProspectSignatoryAsync(int prospectId, int contactId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(
            $"api/prospects/{prospectId}/signatories/{contactId}/exists",
            ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<bool>(ProspectExperienceJsonOptions.Default, ct);
    }

    /// <inheritdoc />
    public async Task CompleteStepAsync(int accountId, string stepName, CancellationToken ct)
    {
        await this.CompleteStepAsync(accountId, stepName, ct, null);
    }

    /// <inheritdoc />
    public async Task CompleteStepAsync(int accountId, string stepName, CancellationToken ct, int? currentUserId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/prospects/{accountId}/onboarding/steps/{Uri.EscapeDataString(stepName)}/complete");
        if (currentUserId.HasValue)
        {
            request.Headers.Add("CurrentUser", currentUserId.Value.ToString());
        }

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task ResetStepAsync(int prospectId, string stepName, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/onboarding/{prospectId}/reset-step",
            new { Step = stepName },
            ProspectExperienceJsonOptions.Default,
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task MarkPaymentMethodInProgressAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(
            $"api/onboarding/{prospectId}/in-progress",
            content: null,
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task SendPaymentPreferenceNotificationsAsync(
        int prospectId,
        string signatoryEmail,
        string[] collabReceiversEmails,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/prospects/{prospectId}/payment-preferences/notifications",
            new PaymentPreferenceNotificationRequest
            {
                SignatoryEmail = signatoryEmail,
                CollabReceiversEmails = collabReceiversEmails
            },
            ProspectExperienceJsonOptions.Default,
            ct);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct)
    {
        logger.LogInformation("Creating prospect for SIRET {Siret} with legal name {LegalName}", inpi.Siret, inpi.LegalName);
        var Region = !string.IsNullOrWhiteSpace(inpi.RegionCode) ? inpi.RegionCode : request.Region;
        var payload = new
        {
            inpi.LegalName,
            inpi.Siret,
            request.LegalForm,
            request.LegalStructure,
            inpi.NafCode,
            inpi.Address,
            inpi.ZipCode,
            inpi.City,
            request.Department,
            Region,
            request.Country,
            request.CaseManagerContactId,
            request.AccountManagerContactId,
            Signatory = BuildSignatoryPayload(request.Signatory)
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/prospects")
        {
            Content = JsonContent.Create(payload, options: ProspectExperienceJsonOptions.Default)
        };
        if (request.CaseManagerContactId.HasValue)
        {
            httpRequest.Headers.Add("CurrentUser", request.CaseManagerContactId.Value.ToString());
        }

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateProspectApiResponse>(ProspectExperienceJsonOptions.Default, ct)
                      ?? throw new HttpRequestException("Prospect creation response was empty.");

        logger.LogInformation("Successfully created prospect with ID {ProspectId} for SIRET {Siret}", created.ProspectId, inpi.Siret);

        return created.ProspectId;
    }

    /// <inheritdoc />
    public async Task<FinalizeProspectOutcome> UpdateProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, CancellationToken ct)
    {
        var payload = new
        {
            AccountId = accountId,
            AccountNumber = accountNumber,
            SignatoryContactId = contactId
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/prospects/{prospectId}")
        {
            Content = JsonContent.Create(payload, options: ProspectExperienceJsonOptions.Default)
        };
        httpRequest.Headers.Add("CurrentUser", contactId.ToString());

        var response = await httpClient.SendAsync(httpRequest, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => FinalizeProspectOutcome.Updated,
            HttpStatusCode.Conflict => FinalizeProspectOutcome.SynchronizationPending,
            HttpStatusCode.NotFound => FinalizeProspectOutcome.ProspectNotFound,
            HttpStatusCode.UnprocessableEntity => FinalizeProspectOutcome.InvalidCreationStatus,
            _ => throw new HttpRequestException(
                $"Unexpected status {(int)response.StatusCode} while finalizing prospect {prospectId}.")
        };
    }

    /// <inheritdoc />
    public async Task<bool> PrepareCreationResumeAsync(int prospectId, int currentUserId, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/prospects/{prospectId}/prepare-creation-resume");
        httpRequest.Headers.Add("CurrentUser", currentUserId.ToString());

        var response = await httpClient.SendAsync(httpRequest, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<ProspectRoleSynchronizationOutcome> GetCreationRoleSynchronizationOutcomeAsync(int prospectId, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/prospects/{prospectId}/creation-role-synchronization", ct);
        return response.StatusCode switch
        {
            HttpStatusCode.NoContent => ProspectRoleSynchronizationOutcome.Synchronized,
            HttpStatusCode.Conflict => ProspectRoleSynchronizationOutcome.SynchronizationPending,
            HttpStatusCode.NotFound => ProspectRoleSynchronizationOutcome.ProspectNotFound,
            _ => throw new HttpRequestException(
                $"Unexpected status {(int)response.StatusCode} while checking role synchronization for prospect {prospectId}.")
        };
    }

    /// <inheritdoc />
    public async Task<bool> PersistBeneficiariesAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(
            $"api/prospects/{prospectId}/beneficiaries",
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
    public async Task<bool> UpdateCreationProgressAsync(int prospectId, int currentUserId, UpdateProspectCreationProgressRequest request, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/prospects/{prospectId}/creation-progress")
        {
            Content = JsonContent.Create(request, options: ProspectExperienceJsonOptions.Default)
        };
        httpRequest.Headers.Add("CurrentUser", currentUserId.ToString());

        var response = await httpClient.SendAsync(httpRequest, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkProspectCreationFailedAsync(int prospectId, int currentUserId, MarkProspectCreationFailedRequest request, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/prospects/{prospectId}/creation-failure")
        {
            Content = JsonContent.Create(request, options: ProspectExperienceJsonOptions.Default)
        };
        httpRequest.Headers.Add("CurrentUser", currentUserId.ToString());

        var response = await httpClient.SendAsync(httpRequest, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<int?> GetProspectIdByAccountIdAsync(int accountId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"api/prospects/account/{accountId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetProspectByAccountIdResponse>(ProspectExperienceJsonOptions.Default, ct);
        return result?.ProspectId;
    }

    /// <inheritdoc />
    public async Task<int?> GetAccountIdByProspectIdAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"api/prospects/{prospectId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetDetailedProspectResponse>(ProspectExperienceJsonOptions.Default, ct);
        if (result is null)
        {
            throw new HttpRequestException($"Empty response from GET api/prospects/{prospectId}");
        }
        return result.AccountId;
    }

    /// <inheritdoc />
    public async Task<string?> GetAkuiteoAccountNumberByProspectIdAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"api/prospects/{prospectId}/akuiteo-account-number", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProspectAkuiteoAccountNumberResponse>(ProspectExperienceJsonOptions.Default, ct);
        return result?.AccountNumber;
    }

    /// <inheritdoc />
    public Task<CommercialProposalEligibilityResponse?> GetCommercialProposalEligibilityAsync(int accountId, int currentUserId, CancellationToken ct)
        => GetEligibilityAsync<CommercialProposalEligibilityResponse>($"api/accounts/{accountId}/commercial-proposal/eligibility", currentUserId, ct);

    /// <inheritdoc />
    public Task SendCommercialProposalAsync(int accountId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
        => SendFileAsync($"api/accounts/{accountId}/commercial-proposal", currentUserId, contactEmail, file, ct);

    /// <inheritdoc />
    public Task<EngagementLetterEligibilityResponse?> GetEngagementLetterEligibilityAsync(int accountId, int currentUserId, CancellationToken ct)
        => GetEligibilityAsync<EngagementLetterEligibilityResponse>($"api/accounts/{accountId}/engagement-letter/eligibility", currentUserId, ct);

    /// <inheritdoc />
    public Task SendEngagementLetterAsync(int accountId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
        => SendFileAsync($"api/accounts/{accountId}/engagement-letter", currentUserId, contactEmail, file, ct);

    private async Task<T?> GetEligibilityAsync<T>(string url, int currentUserId, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("CurrentUser", currentUserId.ToString());
        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ProspectExperienceJsonOptions.Default, ct);
    }

    private async Task SendFileAsync(string url, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", Path.GetFileName(file.FileName));
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("CurrentUser", currentUserId.ToString());
        if (!string.IsNullOrWhiteSpace(contactEmail))
            request.Headers.Add("ContactEmail", contactEmail);
        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<UploadSupportingDocumentResult> UploadSupportingDocumentAsync(
        int prospectId,
        int currentUserId,
        string? contactEmail,
        UploadSupportingDocumentRequest request,
        CancellationToken ct)
    {
        // Resolve prospectId → accountId (Prospect routes use accountId)
        var accountId = await GetAccountIdByProspectIdAsync(prospectId, ct);
        if (!accountId.HasValue)
        {
            return UploadSupportingDocumentResult.ProspectNotFound();
        }

        using var content = new MultipartFormDataContent();

        // Add document type
        content.Add(new StringContent(request.DocumentType), "documentType");

        // Add file
        await using var stream = request.File.OpenReadStream();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.File.ContentType);
        content.Add(streamContent, "file", Path.GetFileName(request.File.FileName));

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/prospects/{accountId.Value}/supporting-documents")
        {
            Content = content
        };
        httpRequest.Headers.Add("CurrentUser", currentUserId.ToString());
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            httpRequest.Headers.Add("ContactEmail", contactEmail);
        }

        var response = await httpClient.SendAsync(httpRequest, ct);

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            var problem = await ReadProblemDetailsAsync(response, ct);
            return UploadSupportingDocumentResult.FileTooLarge(
                problem.ErrorCode ?? ProblemDetailsKeys.DefaultFileTooLargeCode,
                problem.Detail ?? ProblemDetailsKeys.DefaultFileTooLargeMessage);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await ReadProblemDetailsAsync(response, ct);
            return UploadSupportingDocumentResult.ValidationError(
                problem.Field ?? ProblemDetailsKeys.DefaultField,
                problem.ErrorCode ?? ProblemDetailsKeys.DefaultErrorCode,
                problem.Detail ?? ProblemDetailsKeys.DefaultValidationMessage);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UploadSupportingDocumentResult.ProspectNotFound();
        }

        response.EnsureSuccessStatusCode();

        var uploadResponse = await response.Content.ReadFromJsonAsync<UploadSupportingDocumentApiResponse>(ProspectExperienceJsonOptions.Default, ct);

        if (uploadResponse is null || uploadResponse.DocumentId == 0)
        {
            throw new HttpRequestException(
                "Prospect API returned 201 but response body missing or invalid documentId");
        }

        return UploadSupportingDocumentResult.Success(uploadResponse.DocumentId);
    }

    /// <inheritdoc />
    public async Task<DocumentRequirementsResponse?> GetDocumentRequirementsAsync(
        int prospectId,
        CancellationToken ct)
    {
        // Resolve prospectId → accountId (Prospect routes use accountId)
        var accountId = await GetAccountIdByProspectIdAsync(prospectId, ct);
        if (!accountId.HasValue)
        {
            return null;
        }

        var response = await httpClient.GetAsync(
            $"api/prospects/{accountId.Value}/supporting-documents",
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentRequirementsResponse>(ProspectExperienceJsonOptions.Default, ct);
    }

    /// <inheritdoc />
    public async Task<bool> CleanupOnboardingAsync(int accountId, CancellationToken ct)
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
    /// Reads a ProblemDetails response while preserving extension fields serialized as top-level JSON properties.
    /// </summary>
    /// <param name="response">The HTTP response containing problem details.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The parsed problem details values used by Gateway error mapping.</returns>
    private static async Task<(string? Detail, string? Field, string? ErrorCode)> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, null, null);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }

        using (document)
        {
            var root = document.RootElement;

            var detail = TryGetString(root, "detail");
            var field = TryGetString(root, ProblemDetailsKeys.Field);
            var errorCode = TryGetString(root, ProblemDetailsKeys.ErrorCode);

            if (root.TryGetProperty("extensions", out var extensions)
                && extensions.ValueKind == JsonValueKind.Object)
            {
                field ??= TryGetString(extensions, ProblemDetailsKeys.Field);
                errorCode ??= TryGetString(extensions, ProblemDetailsKeys.ErrorCode);
            }

            if (field is null
                && root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object)
            {
                var firstError = errors.EnumerateObject().FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstError.Name))
                {
                    field = firstError.Name;
                }

                if (detail is null
                    && firstError.Value.ValueKind == JsonValueKind.Array
                    && firstError.Value.GetArrayLength() > 0)
                {
                    detail = firstError.Value[0].GetString();
                }
            }

            return (detail, field, errorCode);
        }
    }

    /// <summary>
    /// Reads a string property from a JSON element when it exists.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value, or null when it is absent or not a scalar value.</returns>
    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static object BuildSignatoryPayload(SignatoryDto signatory) => new
    {
        signatory.Title,
        signatory.LastName,
        signatory.FirstName,
        signatory.JobTitle,
        ContactDepartment = signatory.Department,
        signatory.CompanyRole,
        signatory.Email,
        signatory.MobilePhone,
        ContactTypes = signatory.ContactTypes ?? []
    };

    private static string ExtractFileName(HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return fileName?.Trim('"') ?? string.Empty;
    }

    private sealed class CreateProspectApiResponse
    {
        public int ProspectId { get; set; }
    }

    private sealed class UploadSupportingDocumentApiResponse
    {
        public required int DocumentId { get; init; }
    }
}
