namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the Prospect payment preference notification request.
/// </summary>
public sealed class PaymentPreferenceNotificationRequest
{
    /// <summary>
    /// Gets or sets the signatory email address.
    /// </summary>
    public string SignatoryEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collaborator or BS receiver email addresses.
    /// </summary>
    public string[] CollabReceiversEmails { get; set; } = [];
}
