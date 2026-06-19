using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Orchestrates Gateway payment preference flows.
/// </summary>
public interface IPaymentPreferencesOrchestrationService
{
    /// <summary>
    /// Gets the current payment preference for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The payment preference response, or null when the prospect or account is not found.</returns>
    Task<PaymentPreferenceResponse?> GetAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Sets a prospect payment preference to OTHER.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="contactEmail">The authenticated user email.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when the preference was saved and the step completed; otherwise false when the prospect or account is not found.</returns>
    Task<bool> SetOtherAsync(int prospectId, string contactEmail, int currentUserId, CancellationToken ct);

    /// <summary>
    /// Resets a prospect payment preference and resets the payment method onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when the preference and step were reset; otherwise false when the prospect, account, or preference is not found.</returns>
    Task<bool> ResetAsync(int prospectId, CancellationToken ct);
}
