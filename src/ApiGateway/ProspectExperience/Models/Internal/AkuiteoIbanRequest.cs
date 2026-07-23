namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents IBAN components sent through Registry to Akuiteo.
/// </summary>
public sealed class AkuiteoIbanRequest
{
    /// <summary>Gets or sets the IBAN country code.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Gets or sets the IBAN check digits.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank-account part of the IBAN.</summary>
    public string AccountNumber { get; set; } = string.Empty;
}
