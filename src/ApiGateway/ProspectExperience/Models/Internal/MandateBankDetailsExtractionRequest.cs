namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the IBAN and BIC sent to Mandat for banking-details extraction.
/// </summary>
public sealed class MandateBankDetailsExtractionRequest
{
    /// <summary>Gets or sets the IBAN.</summary>
    public string Iban { get; set; } = string.Empty;

    /// <summary>Gets or sets the BIC.</summary>
    public string Bic { get; set; } = string.Empty;
}
