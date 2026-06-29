namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Orchestrates payment preference notification dispatch.
/// </summary>
public interface IPaymentPreferenceNotificationService
{
    /// <summary>
    /// Gets the configured collaborator or BS email receivers.
    /// </summary>
    /// <returns>The configured email receivers.</returns>
    string[] GetCollabEmailReceivers();

    /// <summary>
    /// Sends payment preference notifications through Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="signatoryEmail">The signatory email address.</param>
    /// <param name="collabEmailReceivers">The collaborator or BS receiver email addresses.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendAsync(
        int prospectId,
        string signatoryEmail,
        string[] collabEmailReceivers,
        CancellationToken ct);
}
