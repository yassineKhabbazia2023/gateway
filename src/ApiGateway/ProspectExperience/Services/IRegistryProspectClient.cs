using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public interface IRegistryProspectClient
{
    Task<bool> SiretExistsInAkuiteoAsync(string siret, CancellationToken ct);

    Task<AkuiteoCustomerCreated> CreateAkuiteoCustomerAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct);

    Task CreateAkuiteoContactAsync(string accountNumber, SignatoryDto signatory, CancellationToken ct);

    /// <summary>
    /// Uploads one prospect document to the Akuitéo account document endpoint.
    /// </summary>
    /// <param name="accountNumber">The Akuitéo account number.</param>
    /// <param name="document">The document content and metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Akuitéo accepted the document; otherwise false.</returns>
    Task<bool> UploadAkuiteoDocumentAsync(string accountNumber, ProspectDocumentContentResponse document, CancellationToken ct);

    /// <summary>
    /// Adds extracted SEPA banking information to an Akuiteo account through Registry.
    /// </summary>
    /// <param name="accountId">The Registry account identifier.</param>
    /// <param name="request">The extracted banking-information request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Registry accepted the banking information; otherwise false.</returns>
    Task<bool> UpdateAkuiteoBankingInformationAsync(
        int accountId,
        AkuiteoBankingInformationRequest request,
        CancellationToken ct);

    /// <summary>
    /// Sets the Akuiteo account payment method to direct debit through Registry.
    /// </summary>
    /// <param name="accountId">The Registry account identifier.</param>
    /// <param name="request">The exact account payment-method patch.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Registry accepted the account patch; otherwise false.</returns>
    Task<bool> PatchAkuiteoAccountPaymentMethodAsync(
        int accountId,
        AkuiteoAccountPaymentMethodRequest request,
        CancellationToken ct);
}
