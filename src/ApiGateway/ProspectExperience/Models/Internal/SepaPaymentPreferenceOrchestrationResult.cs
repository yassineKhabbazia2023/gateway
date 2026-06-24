namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Carries the result of Gateway SEPA payment preference orchestration.
/// </summary>
/// <param name="Outcome">The orchestration outcome.</param>
/// <param name="SignatureUrl">The signature URL when orchestration completed.</param>
public sealed record SepaPaymentPreferenceOrchestrationResult(
    SepaPaymentPreferenceOrchestrationOutcome Outcome,
    string? SignatureUrl)
{
    /// <summary>
    /// Creates a completed orchestration result.
    /// </summary>
    /// <param name="signatureUrl">The signature URL.</param>
    /// <returns>The completed result.</returns>
    public static SepaPaymentPreferenceOrchestrationResult Completed(string signatureUrl)
    {
        return new SepaPaymentPreferenceOrchestrationResult(
            SepaPaymentPreferenceOrchestrationOutcome.Completed,
            signatureUrl);
    }

    /// <summary>
    /// Creates an orchestration result without a signature URL.
    /// </summary>
    /// <param name="outcome">The non-completed outcome.</param>
    /// <returns>The result.</returns>
    public static SepaPaymentPreferenceOrchestrationResult FromOutcome(
        SepaPaymentPreferenceOrchestrationOutcome outcome)
    {
        return new SepaPaymentPreferenceOrchestrationResult(outcome, null);
    }
}
