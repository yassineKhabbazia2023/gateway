namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents BIC components sent through Registry to Akuiteo.
/// </summary>
public sealed class AkuiteoBicRequest
{
    /// <summary>Gets or sets the BIC country code.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Gets or sets the BIC bank code.</summary>
    public string Bank { get; set; } = string.Empty;

    /// <summary>Gets or sets the BIC location code.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional BIC branch code.</summary>
    public string? Branch { get; set; }
}
