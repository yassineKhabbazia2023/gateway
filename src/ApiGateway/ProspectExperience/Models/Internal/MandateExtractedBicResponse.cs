namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents BIC components extracted by Mandat.
/// </summary>
public sealed class MandateExtractedBicResponse
{
    /// <summary>Gets or sets the BIC bank code.</summary>
    public string BankCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the BIC country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the BIC location code.</summary>
    public string LocationCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional BIC branch code.</summary>
    public string? BranchCode { get; set; }
}
