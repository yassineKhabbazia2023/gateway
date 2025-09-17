namespace ApiGateway.Models;

public class Summary
{
    public int AccountId { get; set; }

    public Guid? AccountGlobalUniqueId { get; set; }

    public string? AccountNumber { get; set; }

    public string? LegalName { get; set; }

    public bool? IsFavorite { get; set; }

    public bool? IsCustomerRelation { get; set; }

    public string? MissionType { get; set; }

    public int? OfficeId { get; set; }

    public bool IsClarityVisible { get; set; }

    public bool IsSignatory { get; set; }

    public Office? Office { get; set; }

    public Address? Address { get; set; }

    public Contact? Signatory { get; set; }

    public Deployment? Deployment { get; set; }

    public Hub? Hub { get; set; }

    public SubscriptionStatus[] Subscriptions { get; set; } = [];
}