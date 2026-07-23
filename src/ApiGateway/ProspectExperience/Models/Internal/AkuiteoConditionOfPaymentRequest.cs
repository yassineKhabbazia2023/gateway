namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the payment-condition object required by the Akuiteo account patch.
/// </summary>
public sealed class AkuiteoConditionOfPaymentRequest
{
    /// <summary>Gets or sets the payment deadline.</summary>
    public string DeadLine { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment term.</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment day.</summary>
    public int Day { get; set; }
}
