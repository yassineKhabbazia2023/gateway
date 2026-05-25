using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public interface IProspectApiClient
{
    Task<InpiCompanyInfo> GetInpiCompanyInfoAsync(string siret, CancellationToken ct);

    Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct);

    /// <summary>
    /// Creates roles between the specified contacts and account in the prospect service.
    /// </summary>
    /// <param name="requests">The role assignments to create.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateRoleAsync(IReadOnlyCollection<CreateRoleAssignmentRequest> requests, CancellationToken ct);

    Task UpdateProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, CancellationToken ct);
}
