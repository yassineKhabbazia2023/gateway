using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.ProspectExperience.Helpers;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.ProspectExperience.Controllers;

/// <summary>
/// Exposes technical Prospect onboarding maintenance endpoints.
/// </summary>
/// <param name="userContext">The connected user context.</param>
/// <param name="featureFlagService">The feature flag service.</param>
/// <param name="identityService">The identity service.</param>
/// <param name="contactService">The contact service.</param>
/// <param name="prospectApiClient">The Prospect API client.</param>
/// <param name="mandateClient">The Mandate API client.</param>
/// <param name="logger">The logger.</param>
[Route("gtw/prospect/api/onboarding")]
[ApiController]
[Authorize]
public sealed class OnboardingMaintenanceController(
    IUserContext userContext,
    IFeatureFlagService featureFlagService,
    IIdentityService identityService,
    IContactService contactService,
    IProspectApiClient prospectApiClient,
    IMandatePaymentPreferencesClient mandateClient,
    ILogger<OnboardingMaintenanceController> logger) : ControllerBase
{
    /// <summary>
    /// Cleans Prospect onboarding data and Mandate payment preference data for an account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>204 when cleanup succeeds, 403 when disabled or unauthorized, 404 when the account cannot be cleaned.</returns>
    [HttpPost("{accountId}/cleanup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CleanupAsync(int accountId, CancellationToken ct)
    {
        if (accountId <= 0)
        {
            return NotFound();
        }

        var userEmail = userContext.User.GetEmail();

        if (!userContext.User.IsCollaborator() || !identityService.ValidateCollaborator(HttpContext))
        {
            logger.LogWarning(
                "[Response]: 403 - [Controller]: OnboardingMaintenanceController - [Function]: Cleanup - [Reason]: Collaborator access denied for account {AccountId}",
                accountId);
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.NotValidCollaboratorCode,
                    ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, userEmail)
                });
        }

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsQaSensitiveEndpointsEnabled, context: FeatureContext.FromEmail(userEmail), ct: ct))
        {
            logger.LogWarning(
                "[Response]: 403 - [Controller]: OnboardingMaintenanceController - [Function]: Cleanup - [Reason]: QA sensitive endpoints are disabled for account {AccountId}",
                accountId);
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.FeatureFlagDisabledCode,
                    ErrorMessage = string.Format(Errors.FeatureFlagDisabledMessage, FeatureFlagKeys.IsQaSensitiveEndpointsEnabled)
                });
        }

        var authorization = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(
            accountId,
            userEmail,
            contactService,
            HttpContext);
        if (authorization.Error is not null)
        {
            return authorization.Error;
        }

        logger.LogInformation("Starting onboarding cleanup orchestration for account {AccountId}", accountId);

        var prospectCleaned = await prospectApiClient.CleanupOnboardingAsync(accountId, ct);
        if (!prospectCleaned)
        {
            logger.LogWarning("Prospect cleanup failed because account {AccountId} was not found", accountId);
            return NotFound();
        }

        var mandateCleaned = await mandateClient.CleanupAsync(accountId, ct);
        if (!mandateCleaned)
        {
            logger.LogWarning("Mandate cleanup failed because account {AccountId} was not found", accountId);
            return NotFound();
        }

        logger.LogInformation("Completed onboarding cleanup orchestration for account {AccountId}", accountId);
        return NoContent();
    }
}
