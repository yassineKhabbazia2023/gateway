namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Holds the mutable technical state carried by Gateway while executing or resuming
/// a prospect creation orchestration.
/// </summary>
public class ProspectCreationRuntimeState
{
    /// <summary>
    /// Gets or sets the current Prospect identifier.
    /// </summary>
    public int? ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the current legal name used to build the Gateway response and the Rydge account payload.
    /// </summary>
    public string? LegalName { get; set; }

    /// <summary>
    /// Gets or sets the Akuiteo account number used across the orchestration.
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the created or resumed Rydge account identifier.
    /// </summary>
    public int? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the created or resumed signatory contact identifier.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the last successfully completed Gateway-owned milestone, when one exists.
    /// </summary>
    public ProspectCreationMilestone? LastCompletedMilestone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Prospect finalization already succeeded.
    /// </summary>
    public bool FinalizationCompleted { get; set; }
}
