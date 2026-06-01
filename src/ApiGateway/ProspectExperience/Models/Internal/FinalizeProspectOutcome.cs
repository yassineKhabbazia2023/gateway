namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the possible outcomes of the prospect finalization call handled by Prospect.
/// </summary>
public enum FinalizeProspectOutcome
{
    /// <summary>
    /// The prospect was finalized successfully.
    /// </summary>
    Updated,

    /// <summary>
    /// The prospect exists but synchronized account or contact data is not available yet.
    /// </summary>
    SynchronizationPending,

    /// <summary>
    /// The prospect does not exist anymore in Prospect.
    /// </summary>
    ProspectNotFound,

    /// <summary>
    /// The prospect lifecycle no longer allows finalization.
    /// </summary>
    InvalidCreationStatus,
}
