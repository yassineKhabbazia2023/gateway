namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Represents the Gateway response for SEPA mandate signature generation.
/// </summary>
public sealed class SepaPaymentPreferenceResponse
{
    /// <summary>
    /// Gets or sets the signature URL.
    /// </summary>
    public string SignatureUrl { get; set; } = string.Empty;
}
