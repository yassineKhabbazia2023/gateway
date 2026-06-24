namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the Prospect response carrying the linked account identifier.
/// </summary>
public sealed class ProspectAccountResponse
{
    /// <summary>
    /// Gets or sets the prospect identifier.
    /// </summary>
    public int ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the linked account identifier.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Gets or sets the signatory email.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signatory first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signatory last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;
}
