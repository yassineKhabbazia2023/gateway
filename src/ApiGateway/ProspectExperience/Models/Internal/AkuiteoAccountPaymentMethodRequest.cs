namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the exact Akuiteo account patch required after a SEPA mandate is signed.
/// </summary>
public sealed class AkuiteoAccountPaymentMethodRequest
{
    /// <summary>Gets or sets the empty payment condition required by Akuiteo.</summary>
    public AkuiteoConditionOfPaymentRequest ConditionOfPayment { get; set; } = new();

    /// <summary>Gets or sets the Akuiteo payment method.</summary>
    public string MethodOfPayment { get; set; } = string.Empty;
}
