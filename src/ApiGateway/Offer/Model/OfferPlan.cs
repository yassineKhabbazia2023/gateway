namespace ApiGateway.Offer.Model;

public class OfferPlan
{
    public int PlanId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public IEnumerable<PlanPricing>? Pricings { get; set; }
}
