using ApiGateway.ProspectExperience.Constants;
using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.ProspectExperience.Policies;

/// <summary>
/// Provides the rules used to decide whether Gateway can resume an incomplete prospect creation
/// and to validate the checkpoint data required by each resume step.
/// </summary>
public static class ProspectResumePolicy
{
    /// <summary>
    /// Determines whether the provided incomplete prospect state is resumable from a persisted checkpoint.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <returns>True when Gateway must use the resume flow; otherwise false.</returns>
    public static bool CanResumeFromCheckpoint(IncompleteProspectCreationState? state)
    {
        return state is not null
            && state.CompletedMilestone is not null
            && (state.CreationStatus != ProspectCreationStatus.Completed
                || state.CompletedMilestone != ProspectCreationMilestone.RolesAssigned);
    }

    /// <summary>
    /// Determines whether Gateway must restore the prospect lifecycle to a finalizable state before resuming.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <returns>True when Prospect must transition the resume state back to pending creation; otherwise false.</returns>
    public static bool RequiresResumePreparation(IncompleteProspectCreationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.CreationStatus != ProspectCreationStatus.Completed;
    }

    /// <summary>
    /// Determines whether the current retry payload still matches the persisted checkpoint payload.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <param name="requestFingerprint">The fingerprint built from the retry request.</param>
    /// <returns>True when the retry payload matches the stored checkpoint payload; otherwise false.</returns>
    public static bool HasMatchingPayload(IncompleteProspectCreationState state, string requestFingerprint)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        return string.Equals(state.ResumeRequestFingerprint, requestFingerprint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the Akuiteo account number required to resume the orchestration after Akuiteo customer creation.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <param name="siret">The target SIRET used for diagnostic messages.</param>
    /// <returns>The required Akuiteo account number.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the checkpoint is incomplete.</exception>
    public static string RequireAccountNumber(IncompleteProspectCreationState state, string siret)
    {
        if (string.IsNullOrWhiteSpace(state.AkuiteoAccountNumber))
        {
            throw new InvalidOperationException($"Prospect {state.ProspectId} for SIRET {siret} cannot resume because the Akuiteo account number is missing.");
        }

        return state.AkuiteoAccountNumber;
    }

    /// <summary>
    /// Gets the Rydge account identifier required to resume after the account creation step.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <param name="siret">The target SIRET used for diagnostic messages.</param>
    /// <param name="lastCompletedStep">The last completed step stored in the checkpoint.</param>
    /// <returns>The required Rydge account identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the checkpoint is incomplete.</exception>
    public static int RequireAccountId(IncompleteProspectCreationState state, string siret, int lastCompletedStep)
    {
        if (!state.PendingAccountId.HasValue)
        {
            throw new InvalidOperationException($"Prospect {state.ProspectId} for SIRET {siret} cannot resume from step {lastCompletedStep} because the Rydge account identifier is missing.");
        }

        return state.PendingAccountId.Value;
    }

    /// <summary>
    /// Gets the signatory contact identifier required to resume after the contact creation step.
    /// </summary>
    /// <param name="state">The incomplete prospect state returned by Prospect.</param>
    /// <param name="siret">The target SIRET used for diagnostic messages.</param>
    /// <param name="lastCompletedStep">The last completed step stored in the checkpoint.</param>
    /// <returns>The required signatory contact identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the checkpoint is incomplete.</exception>
    public static int RequireSignatoryContactId(IncompleteProspectCreationState state, string siret, int lastCompletedStep)
    {
        if (!state.PendingSignatoryContactId.HasValue)
        {
            throw new InvalidOperationException($"Prospect {state.ProspectId} for SIRET {siret} cannot resume from step {lastCompletedStep} because the signatory contact identifier is missing.");
        }

        return state.PendingSignatoryContactId.Value;
    }
}
