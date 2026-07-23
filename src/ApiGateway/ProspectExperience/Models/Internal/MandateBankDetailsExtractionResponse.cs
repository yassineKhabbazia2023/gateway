namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents banking information extracted by Mandat from an IBAN and BIC.
/// </summary>
public sealed class MandateBankDetailsExtractionResponse
{
    /// <summary>
    /// Gets or sets the extracted IBAN components.
    /// </summary>
    public MandateExtractedIbanResponse Iban { get; set; } = null!;

    /// <summary>
    /// Gets or sets the extracted RIB components.
    /// </summary>
    public MandateExtractedRibResponse Rib { get; set; } = null!;

    /// <summary>
    /// Gets or sets the extracted BIC components.
    /// </summary>
    public MandateExtractedBicResponse Bic { get; set; } = null!;

    /// <summary>
    /// Gets or sets the extracted bank domiciliation.
    /// </summary>
    public string Domiciliation { get; set; } = string.Empty;
}
