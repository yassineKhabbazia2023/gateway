using ApiGateway.Account;
using ApiGateway.Contact;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Orchestrates the Prospect create-password experience while preserving the default Contact flow.
/// </summary>
public class CreatePasswordExperienceService(
    IFeatureFlagService featureFlagService,
    IAccountService accountService,
    IContactService contactService,
    ILogger<CreatePasswordExperienceService> logger) : ICreatePasswordExperienceService
{
    /// <inheritdoc />
    public async Task<HttpResponseMessage> CreateNewPasswordAsync(CreatePasswordExperienceRequest request, CancellationToken ct)
    {
        // First-time password creation is anonymous, so there is no bearer token to extract an email from.
        // ContactId is the only reliable identity we have in that case, so resolve the email through it.
        var resolvedContactEmail = await ResolveContactEmailAsync(request.contactId, ct);
        var contactRequest = request.ToContactRequest();
        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, context: FeatureContext.FromEmail(resolvedContactEmail), ct: ct))
        {
            return await contactService.CreateNewPasswordAsync(contactRequest, entityType: null, ct);
        }

        logger.LogInformation(
            "Evaluating Prospect create-password experience for ContactId {ContactId}",
            request.contactId);

        if (!request.contactId.HasValue)
        {
            logger.LogWarning(
                "Prospect create-password experience enabled but ContactId was not provided in the request payload. Falling back to default create-password flow.");
            return await contactService.CreateNewPasswordAsync(contactRequest, entityType: null, ct);
        }

        var contactId = request.contactId.Value;
        var prospectOnlyResult = await accountService.GetProspectOnlyContactResultAsync(contactId, ct);
        return prospectOnlyResult switch
        {
            ProspectOnlyContactResult.ProspectOnly => await CreateProspectPasswordAsync(contactRequest, contactId, ct),
            ProspectOnlyContactResult.NotProspectOnly => await CreateDefaultPasswordAsync(
                contactRequest,
                contactId,
                "Account returned false for prospect-only check.",
                ct),
            ProspectOnlyContactResult.NotFound => await CreateDefaultPasswordAsync(
                contactRequest,
                contactId,
                "Account returned 404 for prospect-only check.",
                ct),
            _ => throw new InvalidOperationException($"Unsupported prospect-only result: {prospectOnlyResult}")
        };
    }

    /// <summary>
    /// Resolves the contact email to forward downstream via a Contact lookup by <paramref name="contactId"/>,
    /// since the create-password flow is anonymous and never carries a bearer token.
    /// </summary>
    /// <param name="contactId">The contact identifier from the request payload, if any.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved contact email, or <see langword="null"/> if it cannot be determined.</returns>
    private async Task<string?> ResolveContactEmailAsync(int? contactId, CancellationToken ct)
    {
        if (!contactId.HasValue)
        {
            return null;
        }

        var contact = await contactService.GetContactByIdAsync(contactId.Value);
        return contact?.Email;
    }

    /// <summary>
    /// Calls Contact with the Prospect entity type.
    /// </summary>
    /// <param name="request">The create-new-password request.</param>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw Contact downstream response.</returns>
    private async Task<HttpResponseMessage> CreateProspectPasswordAsync(CreateNewPasswordRequest request, int contactId, CancellationToken ct)
    {
        logger.LogInformation(
            "Account returned true for prospect-only check. Calling Contact create-password with entityType PROSPECT for ContactId {ContactId}",
            contactId);
        return await contactService.CreateNewPasswordAsync(request, AccountType.PROSPECT.ToString(), ct);
    }

    /// <summary>
    /// Calls Contact without an entity type.
    /// </summary>
    /// <param name="request">The create-new-password request.</param>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="reason">The fallback reason.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw Contact downstream response.</returns>
    private async Task<HttpResponseMessage> CreateDefaultPasswordAsync(
        CreateNewPasswordRequest request,
        int contactId,
        string reason,
        CancellationToken ct)
    {
        logger.LogInformation(
            "{Reason} Falling back to default create-password flow for ContactId {ContactId}",
            reason,
            contactId);
        return await contactService.CreateNewPasswordAsync(request, entityType: null, ct);
    }
}
