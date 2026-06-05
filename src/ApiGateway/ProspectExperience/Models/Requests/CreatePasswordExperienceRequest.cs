namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Represents the Gateway create-password experience request.
/// </summary>
/// <param name="contactId">The contact identifier used to evaluate the Prospect create-password flow.</param>
/// <param name="token">The password reset token.</param>
/// <param name="newPassword">The new password.</param>
/// <param name="isCreatePasswordAction">Indicates whether the request is part of the create-password action.</param>
public record CreatePasswordExperienceRequest(int? contactId, string token, string newPassword, bool? isCreatePasswordAction = true)
{
    /// <summary>
    /// Converts this Gateway request into the Contact downstream request contract.
    /// </summary>
    /// <returns>A Contact create-new-password request without Gateway-only fields.</returns>
    public CreateNewPasswordRequest ToContactRequest()
    {
        return new CreateNewPasswordRequest(token, newPassword, isCreatePasswordAction ?? true);
    }
}
