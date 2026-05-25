using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public class RegistryProspectClient(HttpClient httpClient) : IRegistryProspectClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<bool> SiretExistsInAkuiteoAsync(string siret, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/prospects/check-eligibility?siret={Uri.EscapeDataString(siret)}", ct);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.Accepted => false,
            _ => throw new HttpRequestException($"Unexpected status {(int)response.StatusCode} from Registry eligibility check for SIRET {siret}.")
        };
    }

    public async Task<AkuiteoCustomerCreated> CreateAkuiteoCustomerAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct)
    {
        var payload = new
        {
            inpi.LegalName,
            inpi.Siret,
            Siren = string.IsNullOrWhiteSpace(inpi.Siren) ? inpi.Siret[..Math.Min(9, inpi.Siret.Length)] : inpi.Siren,
            request.LegalStructure,
            request.LegalForm,
            inpi.NafCode,
            inpi.Address,
            inpi.ZipCode,
            inpi.City,
            DepartmentCode = request.Department,
            RegionCode = request.Region,
            CountryCode = request.Country,
            request.CaseManagerContactId,
            request.AccountManagerContactId
        };

        var response = await httpClient.PostAsJsonAsync("api/akuiteo/customers", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AkuiteoCustomerCreated>(JsonOptions, ct)
                      ?? throw new HttpRequestException("Akuiteo customer creation response was empty.");
        return created;
    }

    public async Task CreateAkuiteoContactAsync(string accountNumber, SignatoryDto signatory, CancellationToken ct)
    {
        var payload = new
        {
            AccountNumber = accountNumber,
            signatory.Title,
            signatory.LastName,
            signatory.FirstName,
            signatory.JobTitle,
            ContactDepartment = signatory.Department,
            signatory.CompanyRole,
            ContactTypes = BuildContactTypes(signatory.ContactTypes),
            signatory.Email,
            signatory.MobilePhone
        };

        var response = await httpClient.PostAsJsonAsync("api/akuiteo/contacts", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    private static object BuildContactTypes(IEnumerable<string>? contactTypes)
    {
        var values = contactTypes ?? [];
        return new
        {
            IsDigitalVaultContact = values.Any(v => string.Equals(v, "COFFRE_FORT_NUMERIQUE", StringComparison.OrdinalIgnoreCase)),
            IsDebtCollectionContact = values.Any(v => string.Equals(v, "RECOUVREMENT", StringComparison.OrdinalIgnoreCase)),
            IsMandateSignatory = values.Any(v => string.Equals(v, "SIGNATAIRE", StringComparison.OrdinalIgnoreCase))
        };
    }
}
