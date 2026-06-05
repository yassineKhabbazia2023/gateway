using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Orchestrates the Prospect create-password experience decision flow.
/// </summary>
public interface ICreatePasswordExperienceService
{
    /// <summary>
    /// Creates a new password by selecting the default Contact flow or the Prospect template flow.
    /// </summary>
    /// <param name="request">The Gateway create-password experience request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw Contact downstream response.</returns>
    Task<HttpResponseMessage> CreateNewPasswordAsync(CreatePasswordExperienceRequest request, CancellationToken ct);
}
