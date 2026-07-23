namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents domestic banking details sent through Registry to Akuiteo.
/// </summary>
public sealed class AkuiteoBankDetailsRequest
{
    /// <summary>Gets or sets the bank entity code.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>Gets or sets the branch counter code.</summary>
    public string Counter { get; set; } = string.Empty;

    /// <summary>Gets or sets the domestic account number.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the RIB key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the bank domiciliation.</summary>
    public string Domiciliation { get; set; } = string.Empty;
}
