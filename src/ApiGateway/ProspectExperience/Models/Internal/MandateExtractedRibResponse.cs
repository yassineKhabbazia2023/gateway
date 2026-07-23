namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents RIB components extracted by Mandat.
/// </summary>
public sealed class MandateExtractedRibResponse
{
    /// <summary>Gets or sets the bank code.</summary>
    public string BankCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the branch code.</summary>
    public string BranchCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the domestic account number.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the RIB key.</summary>
    public string RibKey { get; set; } = string.Empty;
}
