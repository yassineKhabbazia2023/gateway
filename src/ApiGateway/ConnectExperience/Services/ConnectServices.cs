using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.ConnectExperience.Models;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Models;
using ApiGateway.Offer;
using ApiGateway.ProspectExperience.Enum;
using Pulse.ExceptionMiddleware.Exceptions;

namespace ApiGateway.ConnectExperience.Services;

public class ConnectServices(
    IContactService contactService,
    IAuthorizationService authorizationService,
    IOfferService offerService,
    IAccountService accountService,
    IFeatureFlagService featureFlagService,
    ILogger<ConnectServices> logger) : IConnectServices
{
    public async Task<UserInformation> GetUserInformation(string userEmail)
    {
        // Get contact's information
        var contact = await contactService.GetContactAsync(userEmail) ?? throw new BadRequestException(Errors.NotFoundContactCode, Errors.NotFoundContactMessage);

        // Get contact's authorization global
        var authorizationGlobal = await authorizationService.GetContactAuthorizationAsync(contact.Id, null);

        // Get contact's favorite account
        var favoriteAccounts = await accountService.GetFavoriteAccountsByContactIdAsync(contact.Id);

        return new UserInformation
        {
            Contact = new(
                contact.Id, 
                contact.FirstName, 
                contact.LastName, 
                contact.Email, 
                contact.LandPhone, 
                contact.MobilePhone, 
                contact.OldId, 
                contact.Type, 
                contact.Persona),
            Permissions = authorizationGlobal,
            FavoriteEntities = favoriteAccounts == null ?[] : [.. favoriteAccounts]
        };
    }

    public async Task<Summary?> GetSummaryAsync(int accountId, int currentUserId, string contactType)
    {
        var summaryTask = accountService.GetSummaryAsync(accountId, currentUserId, contactType);
        var subscriptionsTask = offerService.GetSubscriptionsAsync(accountId);

        await Task.WhenAll(summaryTask, subscriptionsTask);

        var summary = await summaryTask;
        var subscriptions = await subscriptionsTask;

        if (summary is null)
        {
            throw new BadRequestException(Errors.NotFoundAccountCode, string.Format(Errors.NotFoundAccountMessage, accountId));
        }

        if (subscriptions is null)
        {
            throw new BadRequestException(Errors.NotFoundAccountCode, string.Format(Errors.NotFoundAccountMessage, accountId));
        }

        summary.Subscriptions = subscriptions;
        return summary;
    }

    public async Task<IEnumerable<int>> SendEmailAsync(string userEmail, int accountId, int[] customerIDs, string? entityType = null)
    {
        var contact = await contactService.GetContactAsync(userEmail)
            ?? throw new BadRequestException(Errors.NotFoundContactCode, Errors.NotFoundContactMessage);
        var account = await accountService.GetAccountAsync(accountId)
            ?? throw new BadRequestException(Errors.NotFoundAccountCode, string.Format(Errors.NotFoundAccountMessage, accountId));

        var isProspectEntityType = string.Equals(entityType, AccountType.PROSPECT.ToString(), StringComparison.OrdinalIgnoreCase);
        var isProspectExperienceEnabled = await featureFlagService.IsEnabledAsync(
            FeatureFlagKeys.IsProspectExperienceEnabled,
            context: FeatureContext.FromEmail(userEmail));

        if (isProspectEntityType && !isProspectExperienceEnabled)
        {
            logger.LogWarning(
                "Prospect bulk invitation rejected because prospect experience feature flag is disabled for account {AccountNumber}",
                account.AccountNumber);
            throw new ForbiddenException(Errors.ProspectExperienceDisabledCode, Errors.ProspectExperienceDisabledMessage);
        }

        var invitedCustomerIDs = await contactService.SendEmailAsync(contact.Id, account.AccountNumber!, customerIDs, entityType);
        await accountService.UpdateLastActivityDateAsync(contact.Id, contact.Type!, accountId);

        return invitedCustomerIDs;
    }
}
