using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.ConnectExperience.Models;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Models;
using ApiGateway.Offer;
using Pulse.ExceptionMiddleware.Exceptions;

namespace ApiGateway.ConnectExperience.Services;

public class ConnectServices(
    IContactService contactService,
    IAuthorizationService authorizationService,
    IOfferService offerService,
    IAccountService accountService) : IConnectServices
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

    public async Task<Summary?> GetSummaryAsync(int accountId, int currentUserId)
    {
        var summaryTask = accountService.GetSummaryAsync(accountId, currentUserId);
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
}
