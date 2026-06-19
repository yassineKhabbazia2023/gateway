using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Defines a strategy able to complete one prospect onboarding step.
/// </summary>
public interface IProspectStepCompletionStrategy
{
    /// <summary>
    /// Gets the strategy priority. Higher values are evaluated first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines whether the strategy supports the provided step name.
    /// </summary>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <returns>True when the strategy supports the step; otherwise false.</returns>
    bool CanHandle(string stepName);

    /// <summary>
    /// Completes the supported onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <returns>The external document upload result.</returns>
    Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct);

    /// <summary>
    /// Completes the supported onboarding step with completion audit metadata.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <returns>The external document upload result.</returns>
    Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct,
        int? currentUserId);
}
