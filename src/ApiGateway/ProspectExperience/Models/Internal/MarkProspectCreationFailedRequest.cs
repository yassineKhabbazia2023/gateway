namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Carries the latest known checkpoint data when Gateway records a pre-finalization creation failure in Prospect.
/// </summary>
public class MarkProspectCreationFailedRequest
{
    /// <summary>
    /// Gets or sets the last successfully completed semantic milestone to persist for resume purposes.
    /// This value can be omitted when the latest persisted checkpoint is still owned directly by Prospect.
    /// </summary>
    public ProspectCreationMilestone? CompletedMilestone { get; set; }

    /// <summary>
    /// Gets or sets the Akuiteo account number created before the failure, when available.
    /// </summary>
    public string? AkuiteoAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the created Rydge account identifier captured before the failure, when available.
    /// </summary>
    public int? PendingAccountId { get; set; }

    /// <summary>
    /// Gets or sets the created signatory contact identifier captured before the failure, when available.
    /// </summary>
    public int? PendingSignatoryContactId { get; set; }
}
