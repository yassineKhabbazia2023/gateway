namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Response model for getting a prospect by ID.
/// </summary>
public class GetProspectResponse
{
    /// <summary>
    /// Gets or sets the prospect identifier.
    /// </summary>
    public int ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the account identifier.
    /// </summary>
    public int AccountId { get; set; }
}
