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
            NafCode = company.ApeCode
        };
    }

    public async Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct)
    {
        logger.LogInformation("Creating prospect for SIRET {Siret} with legal name {LegalName}", inpi.Siret, inpi.LegalName);

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
            request.Region,
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

    /// <inheritdoc/>
    public async Task CreateRoleAsync(IReadOnlyCollection<CreateRoleAssignmentRequest> requests, CancellationToken ct)
    {
        logger.LogInformation("Creating {RoleCount} prospect roles", requests.Count);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/roles")
        {
            Content = JsonContent.Create(requests, options: JsonOptions)
        };

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Prospect roles created or already existed for {RoleCount} assignments", requests.Count);
    }

    public async Task UpdateProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, CancellationToken ct)
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

    private sealed class CreateProspectApiResponse
    {
        public int ProspectId { get; set; }
    }
}
