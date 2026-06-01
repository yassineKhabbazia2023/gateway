namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the possible outcomes of the Prospect role synchronization confirmation call.
/// </summary>
public enum ProspectRoleSynchronizationOutcome
{
    /// <summary>
    /// The expected role rows are available in Prospect.
    /// </summary>
    Synchronized,

    /// <summary>
    /// The expected role rows are not available in Prospect yet.
    /// </summary>
    SynchronizationPending,

    /// <summary>
    /// The prospect does not exist anymore in Prospect.
    /// </summary>
    ProspectNotFound
}
