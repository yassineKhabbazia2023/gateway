using ApiGateway.ProspectExperience.Models.Responses;

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
    Task<PaymentPreferenceResponse?> GetAsync(int accountId, CancellationToken ct);

    /// <summary>
    /// Sets the account payment preference to OTHER.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="contactEmail">The authenticated user email to forward to Mandat.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat saved the preference; otherwise false when the account is not found.</returns>
    Task<bool> SetOtherAsync(int accountId, string contactEmail, CancellationToken ct);

    /// <summary>
    /// Resets the account payment preference.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Mandat reset the preference; otherwise false when the account or preference is not found.</returns>
    Task<bool> ResetAsync(int accountId, CancellationToken ct);
}
