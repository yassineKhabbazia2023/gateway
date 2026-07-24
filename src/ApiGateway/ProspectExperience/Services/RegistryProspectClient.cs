using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiGateway.ProspectExperience.Helpers;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public class RegistryProspectClient(
    HttpClient httpClient,
    ILogger<RegistryProspectClient> logger) : IRegistryProspectClient
{
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
            RegionCode = inpi.RegionCode,
            CountryCode = request.Country,
            request.CaseManagerContactId,
            request.AccountManagerContactId
        };

        var response = await httpClient.PostAsJsonAsync("api/akuiteo/customers", payload, ProspectExperienceJsonOptions.Default, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AkuiteoCustomerCreated>(ProspectExperienceJsonOptions.Default, ct)
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

        var response = await httpClient.PostAsJsonAsync("api/akuiteo/contacts", payload, ProspectExperienceJsonOptions.Default, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<bool> UploadAkuiteoDocumentAsync(string accountNumber, ProspectDocumentContentResponse document, CancellationToken ct)
    {
        using var response = await UploadAkuiteoDocumentCoreAsync(accountNumber, document, ct);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning(
            "Registry Akuiteo document upload failed. AccountNumber: {AccountNumber}, DocumentName: {DocumentName}, ContentType: {ContentType}, StatusCode: {StatusCode}, ResponseBody: {ResponseBody}",
            accountNumber,
            document.FileName,
            document.ContentType,
            (int)response.StatusCode,
            responseBody);
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAkuiteoBankingInformationAsync(
        int accountId,
        AkuiteoBankingInformationRequest request,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/akuiteo/account/{accountId}/banking-informations",
            new[] { request },
            ProspectExperienceJsonOptions.Default,
            ct);

        return IsSuccessfulAccountOperation(
            response,
            accountId,
            "banking information update");
    }

    /// <inheritdoc />
    public async Task<bool> PatchAkuiteoAccountPaymentMethodAsync(
        int accountId,
        AkuiteoAccountPaymentMethodRequest request,
        CancellationToken ct)
    {
        using var response = await httpClient.PatchAsJsonAsync(
            $"api/akuiteo/account/{accountId}",
            request,
            ProspectExperienceJsonOptions.Default,
            ct);

        return IsSuccessfulAccountOperation(
            response,
            accountId,
            "payment method update");
    }

    /// <summary>
    /// Sends the Registry document upload request to Akuiteo.
    /// </summary>
    /// <param name="accountNumber">The Akuitéo account number.</param>
    /// <param name="document">The document content and metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Registry HTTP response.</returns>
    private async Task<HttpResponseMessage> UploadAkuiteoDocumentCoreAsync(string accountNumber, ProspectDocumentContentResponse document, CancellationToken ct)
    {
        using var multipartContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(document.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(document.ContentType);
        multipartContent.Add(fileContent, "document", document.FileName);

        return await httpClient.PostAsync(
            $"api/akuiteo/account/{Uri.EscapeDataString(accountNumber)}/documents",
            multipartContent,
            ct);
    }

    /// <summary>
    /// Maps a Registry Akuiteo account-operation response to the orchestration result.
    /// </summary>
    /// <param name="response">The Registry response.</param>
    /// <param name="accountId">The Registry account identifier.</param>
    /// <param name="operation">The account operation description.</param>
    /// <returns>True for a successful response; otherwise false.</returns>
    private bool IsSuccessfulAccountOperation(
        HttpResponseMessage response,
        int accountId,
        string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        logger.LogWarning(
            "Registry Akuiteo {Operation} failed. AccountId: {AccountId}, StatusCode: {StatusCode}",
            operation,
            accountId,
            (int)response.StatusCode);
        return false;
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
