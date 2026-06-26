namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Prospect data returned from Prospect API.
/// </summary>
public sealed class GetDetailedProspectResponse
{
    /// <summary>
    /// Gets or sets the prospect identifier.
    /// </summary>
    public int ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the account identifier.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Gets or sets the account number.
    /// </summary>
    public required string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the account type.
    /// </summary>
    public required string AccountType { get; set; }

    /// <summary>
    /// Gets or sets the legal name.
    /// </summary>
    public required string LegalName { get; set; }

    /// <summary>
    /// Gets or sets the SIRET number.
    /// </summary>
    public required string Siret { get; set; }

    /// <summary>
    /// Gets or sets the legal form.
    /// </summary>
    public required string LegalForm { get; set; }
}
