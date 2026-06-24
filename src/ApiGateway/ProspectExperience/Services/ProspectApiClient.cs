using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public class ProspectApiClient(HttpClient httpClient, ILogger<ProspectApiClient> logger) : IProspectApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task<InpiCompanyInfo> GetInpiCompanyInfoAsync(string siret, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/accounts/external-companies/{Uri.EscapeDataString(siret)}", ct);
        response.EnsureSuccessStatusCode();
        var company = await response.Content.ReadFromJsonAsync<ExternalCompanyResponse>(JsonOptions, ct)
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
        return await response.Content.ReadFromJsonAsync<IncompleteProspectCreationState>(JsonOptions, ct)
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
        return await response.Content.ReadFromJsonAsync<DocumentsToUploadToExternalServiceResponse>(JsonOptions, ct)
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
    public async Task<DocumentUploadResultResponse?> RegisterDocumentUploadResultAsync(
        int prospectId,
        DocumentUploadResultRequest request,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/prospects/{prospectId}/documents/upload-result",
            request,
            JsonOptions,
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DocumentUploadResultResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException($"Document upload result response for prospect {prospectId} was empty.");
    }

    /// <inheritdoc />
    public async Task<ProspectAccountResponse?> GetProspectAccountAsync(int prospectId, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"api/prospects/{prospectId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProspectAccountResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException($"Prospect account response for prospect {prospectId} was empty.");
    }

    /// <inheritdoc />
    public async Task CompleteStepAsync(int prospectId, string stepName, CancellationToken ct)
    {
        await this.CompleteStepAsync(prospectId, stepName, ct, null);
    }

    /// <inheritdoc />
    public async Task CompleteStepAsync(int prospectId, string stepName, CancellationToken ct, int? currentUserId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/prospects/{prospectId}/onboarding/steps/{Uri.EscapeDataString(stepName)}/complete");
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
            JsonOptions,
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
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        if (request.CaseManagerContactId.HasValue)
        {
            httpRequest.Headers.Add("CurrentUser", request.CaseManagerContactId.Value.ToString());
        }

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateProspectApiResponse>(JsonOptions, ct)
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
            Content = JsonContent.Create(payload, options: JsonOptions)
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
            Content = JsonContent.Create(request, options: JsonOptions)
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
            Content = JsonContent.Create(request, options: JsonOptions)
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
        var result = await response.Content.ReadFromJsonAsync<GetProspectByAccountIdResponse>(JsonOptions, ct);
        return result?.ProspectId;
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
        var result = await response.Content.ReadFromJsonAsync<ProspectAkuiteoAccountNumberResponse>(JsonOptions, ct);
        return result?.AccountNumber;
    }

    /// <inheritdoc />
    public Task<CommercialProposalEligibilityResponse?> GetCommercialProposalEligibilityAsync(int prospectId, int currentUserId, CancellationToken ct)
        => GetEligibilityAsync<CommercialProposalEligibilityResponse>($"api/prospects/{prospectId}/commercial-proposal/eligibility", currentUserId, ct);

    /// <inheritdoc />
    public Task SendCommercialProposalAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
        => SendFileAsync($"api/prospects/{prospectId}/commercial-proposal", currentUserId, contactEmail, file, ct);

    /// <inheritdoc />
    public Task<EngagementLetterEligibilityResponse?> GetEngagementLetterEligibilityAsync(int prospectId, int currentUserId, CancellationToken ct)
        => GetEligibilityAsync<EngagementLetterEligibilityResponse>($"api/prospects/{prospectId}/engagement-letter/eligibility", currentUserId, ct);

    /// <inheritdoc />
    public Task SendEngagementLetterAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
        => SendFileAsync($"api/prospects/{prospectId}/engagement-letter", currentUserId, contactEmail, file, ct);

    private async Task<T?> GetEligibilityAsync<T>(string url, int currentUserId, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("CurrentUser", currentUserId.ToString());
        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
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
}
