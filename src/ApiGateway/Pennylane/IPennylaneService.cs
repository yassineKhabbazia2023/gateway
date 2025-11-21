using ApiGateway.Offer.Model;

namespace ApiGateway.Pennylane;

public interface IPennylaneService
{
    bool ShouldCreateCompanyForOffer(int offerId);

    Task<CreateCompanyResult> CreateCompanyAsync(CreateCompanyRequest request);
}
