namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents one Registry banking-information change for an Akuiteo account.
/// </summary>
public sealed class AkuiteoBankingInformationRequest
{
    /// <summary>Gets or sets the SEPA banking information.</summary>
    public AkuiteoSepaRequest Sepa { get; set; } = null!;

    /// <summary>Gets or sets the non-SEPA banking information.</summary>
    public object? NoneSepa { get; set; }

    /// <summary>Gets or sets the Akuiteo banking-information action.</summary>
    public string Action { get; set; } = string.Empty;
}
