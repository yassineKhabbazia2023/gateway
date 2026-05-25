namespace ApiGateway.ProspectExperience.Models.Contracts;

/// <summary>
/// Represents the company payload returned by the Prospect external company lookup endpoint.
/// </summary>
internal sealed class ExternalCompanyResponse
{
    /// <summary>
    /// Gets or sets the company display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the company SIREN.
    /// </summary>
    public string Siren { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the company SIRET.
    /// </summary>
    public string Siret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the company APE code.
    /// </summary>
    public string ApeCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the company address.
    /// </summary>
    public ExternalCompanyAddressResponse? Address { get; set; }
}
