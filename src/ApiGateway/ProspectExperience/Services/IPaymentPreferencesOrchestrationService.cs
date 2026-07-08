using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

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
    /// <param name="contactEmail">The authenticated user email used for document audit headers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The payment preference response, or null when the prospect or account is not found.</returns>
    Task<PaymentPreferenceResponse?> GetAsync(int prospectId, string? contactEmail, CancellationToken ct);

    /// <summary>
    /// Downloads the signed SEPA mandate content for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The signed mandate document content, or null when unavailable.</returns>
    Task<ProspectDocumentContentResponse?> DownloadSignedSepaMandateAsync(int prospectId, CancellationToken ct);

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
    /// Generates a SEPA mandate signature request for a prospect payment preference.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The multipart SEPA request.</param>
    /// <param name="contactEmail">The authenticated user email.</param>
    /// <param name="contactFirstName">The authenticated contact first name.</param>
    /// <param name="contactLastName">The authenticated contact last name.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The SEPA orchestration result.</returns>
    Task<SepaPaymentPreferenceOrchestrationResult> SetSepaAsync(
        int prospectId,
        SepaPaymentPreferenceRequest request,
        string contactEmail,
        string contactFirstName,
        string contactLastName,
        int currentUserId,
        CancellationToken ct);

    /// <summary>
    /// Resets a prospect payment preference and resets the payment method onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when the preference and step were reset; otherwise false when the prospect, account, or preference is not found.</returns>
    Task<bool> ResetAsync(int prospectId, CancellationToken ct);
}
