using ApiGateway.Offer.Model;
using ApiGateway.Pennylane.Models;

namespace ApiGateway.Pennylane;

public interface IPennylaneService
{
    bool ShouldCreateCompanyForOffer(int offerId);

    Task<CreateCompanyResult> CreateCompanyAsync(CreateCompanyRequest request);

    Task<GrantPennylaneAccessResult> GrantPennylaneAccessAsync(PennylaneAuthorizationRequest request);
}
