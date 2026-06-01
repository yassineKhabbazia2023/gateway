namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the technical Prospect state returned to Gateway so that an incomplete orchestration can resume.
/// </summary>
public class IncompleteProspectCreationState
{
    /// <summary>
    /// Gets or sets the incomplete prospect identifier.
    /// </summary>
    public int ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the current legal name stored on the prospect.
    /// </summary>
    public string LegalName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the current technical lifecycle status.
    /// </summary>
    public int CreationStatus { get; set; }

    /// <summary>
    /// Gets or sets the last successfully completed Gateway orchestration step.
    /// </summary>
    public int LastCompletedStep { get; set; }

    /// <summary>
    /// Gets or sets the last successfully completed Gateway-owned semantic milestone, when one exists.
    /// This field remains null when the latest persisted checkpoint is owned directly by Prospect.
    /// </summary>
    public ProspectCreationMilestone? CompletedMilestone { get; set; }

    /// <summary>
    /// Gets or sets the Akuiteo account number created during the orchestration, when available.
    /// </summary>
    public string? AkuiteoAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the created Rydge account identifier stored for resume purposes, when available.
    /// </summary>
    public int? PendingAccountId { get; set; }

    /// <summary>
    /// Gets or sets the created Rydge signatory contact identifier stored for resume purposes, when available.
    /// </summary>
    public int? PendingSignatoryContactId { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint of the persisted creation payload used to validate resume retries.
    /// </summary>
    public string ResumeRequestFingerprint { get; set; } = default!;
}
