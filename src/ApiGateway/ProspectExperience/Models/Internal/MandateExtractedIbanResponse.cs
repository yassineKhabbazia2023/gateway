namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents IBAN components extracted by Mandat.
/// </summary>
public sealed class MandateExtractedIbanResponse
{
    /// <summary>Gets or sets the IBAN country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the IBAN check digits.</summary>
    public string CheckDigits { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank-account part of the IBAN.</summary>
    public string BankAccountPart { get; set; } = string.Empty;
}
