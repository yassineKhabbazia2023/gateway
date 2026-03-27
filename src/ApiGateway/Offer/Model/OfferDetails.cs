namespace ApiGateway.Offer.Model;

public class OfferDetails
{
    public int OfferId { get; set; }
    public IReadOnlyList<OfferPlan>? Plans { get; set; }
}
