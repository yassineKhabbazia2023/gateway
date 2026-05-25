using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public interface IRegistryProspectClient
{
    Task<bool> SiretExistsInAkuiteoAsync(string siret, CancellationToken ct);

    Task<AkuiteoCustomerCreated> CreateAkuiteoCustomerAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct);

    Task CreateAkuiteoContactAsync(string accountNumber, SignatoryDto signatory, CancellationToken ct);
}
