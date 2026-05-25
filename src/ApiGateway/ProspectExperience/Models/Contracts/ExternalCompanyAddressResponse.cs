namespace ApiGateway.ProspectExperience.Models.Contracts;

/// <summary>
/// Represents the address returned by the Prospect external company lookup endpoint.
/// </summary>
internal sealed class ExternalCompanyAddressResponse
{
    /// <summary>
    /// Gets or sets the first address line.
    /// </summary>
    public string Line1 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the department code.
    /// </summary>
    public string DepartmentCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the region code.
    /// </summary>
    public string RegionCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the country code.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;
}
