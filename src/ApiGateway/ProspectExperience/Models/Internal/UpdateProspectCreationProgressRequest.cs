namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the technical progress checkpoint persisted in Prospect after a successful Gateway creation step.
/// </summary>
public class UpdateProspectCreationProgressRequest
{
    /// <summary>
    /// Gets or sets the last successfully completed semantic milestone reported by Gateway.
    /// </summary>
    public required ProspectCreationMilestone CompletedMilestone { get; set; }

    /// <summary>
    /// Gets or sets the Akuiteo account number created during the orchestration, when available.
    /// </summary>
    public string? AkuiteoAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the created Rydge account identifier, when available.
    /// </summary>
    public int? PendingAccountId { get; set; }

    /// <summary>
    /// Gets or sets the created Rydge signatory contact identifier, when available.
    /// </summary>
    public int? PendingSignatoryContactId { get; set; }
}
