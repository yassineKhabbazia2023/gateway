namespace ApiGateway.ProspectExperience.Constants;

/// <summary>
/// Defines the technical creation lifecycle status values returned by Prospect.
/// </summary>
public static class ProspectCreationStatus
{
    /// <summary>
    /// Gets the status value used while the prospect creation is still in progress.
    /// </summary>
    public const int PendingCreation = 0;

    /// <summary>
    /// Gets the status value used once Prospect finalization has completed successfully.
    /// </summary>
    public const int Completed = 1;

    /// <summary>
    /// Gets the status value used when a pre-finalization orchestration failure has been recorded.
    /// </summary>
    public const int Failed = 2;
}
