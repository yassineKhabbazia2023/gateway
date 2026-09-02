using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.ConnectExperience.Models;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Models;
using ApiGateway.Offer;
using ApiGateway.Offer.Constants;
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

    public async Task<bool> GetShouldDisplaySerenityModalAsync(int contactId, string userEmail)
    {
        var isSerenityRedirectionModalEnabled = await featureFlagService.IsEnabledAsync(
            FeatureFlagKeys.IsSerenityRedirectionModalEnabled,
            context: FeatureContext.FromEmail(userEmail));

        if (!isSerenityRedirectionModalEnabled)
        {
            logger.LogWarning("Serenity modal decision rejected for contact {ContactId}: feature flag disabled", contactId);
            throw new ForbiddenException(Errors.SerenityModalDisabledCode, Errors.SerenityModalDisabledMessage);
        }

        // Account porte les criteres 1 (code de routage cible), 2 (adresse electronique dématérialisée) et 4 (choix deja exprimé),
        // On extrait les critères de ce MS en un appel unique
        var eligibility = await accountService.GetSerenityEligibilityAsync(contactId);

        // API Account injoignable, ou choix deja exprimé (vrai OU faux) : pas de modal.
        if (eligibility is null || eligibility.HasMadeChoice)
        {
            return false;
        }

        var candidates = eligibility.CandidateAccountIds;
        if (candidates.Length == 0)
        {
            return false;
        }

        // Critere 3 : écarter les entites qui ont deja souscrites a Pennylane.
        var subscribed = await offerService.GetAccountIdsWithActiveSubscriptionAsync(candidates, OfferCodes.Pennylane);
        if (subscribed is null)
        {
            // Offer injoignable : fail closed, on ne propose pas la modal sans avoir pu verifier.
            logger.LogWarning("Serenity modal disabled for contact {ContactId}: Offer subscriptions could not be read", contactId);
            return false;
        }

        // Except garantit que les critères 1, 2 et 3 portent sur UNE MEME entite.
        return candidates.Except(subscribed).Any();
    }

    public async Task SetSerenityModalChoiceAsync(int contactId, string userEmail, bool isAccepted)
    {
        var isSerenityRedirectionModalEnabled = await featureFlagService.IsEnabledAsync(
            FeatureFlagKeys.IsSerenityRedirectionModalEnabled,
            context: FeatureContext.FromEmail(userEmail));

        if (!isSerenityRedirectionModalEnabled)
        {
            logger.LogWarning("Serenity modal choice rejected for contact {ContactId}: feature flag disabled", contactId);
            throw new ForbiddenException(Errors.SerenityModalDisabledCode, Errors.SerenityModalDisabledMessage);
        }

        await accountService.SetSerenityChoiceAsync(contactId, isAccepted);
    }
}
