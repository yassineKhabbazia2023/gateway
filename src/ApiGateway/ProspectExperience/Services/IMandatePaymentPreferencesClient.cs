using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Calls Mandat payment preference endpoints.
/// </summary>
public interface IMandatePaymentPreferencesClient
{
    /// <summary>
    /// Gets the payment preference for an account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The payment preference response, or null when the account is not found.</returns>
    Task<MandatePaymentPreferenceResponse?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>
    /// Marks the latest account SEPA mandate as sent to Akuiteo.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat updated the mandate; otherwise false.</returns>
    Task<bool> MarkSentToAkuiteoAsync(int accountId, CancellationToken ct);

    /// <summary>
    /// Saves the uploaded signed mandate Prospect document identifier.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="signedMandateDocumentId">The uploaded signed mandate Prospect document identifier.</param>
    /// <returns>True when Mandat updated the mandate; otherwise false.</returns>
    Task<bool> SaveSignedMandateDocumentIdAsync(int accountId, CancellationToken ct, string signedMandateDocumentId);

    /// <summary>
    /// Gets the uploaded signed mandate Prospect document identifier.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Prospect document identifier, or null when unavailable.</returns>
    Task<string?> GetSignedMandateDocumentIdAsync(int accountId, CancellationToken ct);

    /// <summary>
    /// Sets the account payment preference to OTHER.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="contactEmail">The authenticated user email to forward to Mandat.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat saved the preference; otherwise false when the account is not found.</returns>
    Task<bool> SetOtherAsync(int accountId, string contactEmail, CancellationToken ct);

    /// <summary>
    /// Generates a SEPA mandate signature request for an account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="request">The downstream Mandat request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The signature URL, or null when the account is not found.</returns>
    Task<string?> SetSepaAsync(
        int accountId,
        MandateSepaPaymentPreferenceRequest request,
        CancellationToken ct);

    /// <summary>
    /// Resets the account payment preference.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat reset the preference; otherwise false when the account or preference is not found.</returns>
    Task<bool> ResetAsync(int accountId, CancellationToken ct);

    /// <summary>
    /// Cleans Mandat onboarding preference data for an account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat cleaned the account; otherwise false when the account is not found.</returns>
    Task<bool> CleanupAsync(int accountId, CancellationToken ct);
}
