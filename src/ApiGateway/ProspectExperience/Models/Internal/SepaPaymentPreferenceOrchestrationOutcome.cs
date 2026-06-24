namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents SEPA payment preference orchestration outcomes.
/// </summary>
public enum SepaPaymentPreferenceOrchestrationOutcome
{
    /// <summary>
    /// The SEPA mandate signature flow completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The prospect or account was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The authenticated user is not the signatory linked to the prospect.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The downstream Mandat operation failed.
    /// </summary>
    MandateFailed
}
