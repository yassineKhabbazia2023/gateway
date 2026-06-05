namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Represents the create-new-password request contract exposed by the Contact authentication endpoint.
/// </summary>
/// <param name="token">The password reset token.</param>
/// <param name="newPassword">The new password.</param>
/// <param name="isCreatePasswordAction">Indicates whether the request is part of the create-password action.</param>
public record CreateNewPasswordRequest(string token, string newPassword, bool? isCreatePasswordAction = true);
