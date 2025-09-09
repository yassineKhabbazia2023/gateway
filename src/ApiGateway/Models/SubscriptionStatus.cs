namespace ApiGateway.Models;

public class SubscriptionStatus
{
    public int SubscriptionId { get; set; }

    public int OfferId { get; set; }

    public string? Status { get; set; }

    public int AccountId { get; set; }

    public string? OfferCode { get; set; }

    public string? OfferName { get; set; }

    public DateTime? CreationDate { get; set; }

    public string? Validator { get; set; }
}