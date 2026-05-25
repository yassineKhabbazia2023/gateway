using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

public interface IProspectService
{
    Task<ProspectListItem> CreateAsync(CreateProspectRequest request, CancellationToken ct);
}
