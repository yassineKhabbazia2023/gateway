using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

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

    private sealed class CreateProspectApiResponse
    {
        public int ProspectId { get; set; }
    }
}
