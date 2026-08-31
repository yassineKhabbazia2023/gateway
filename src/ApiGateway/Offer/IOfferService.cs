using ApiGateway.Models;
using ApiGateway.Offer.Model;

namespace ApiGateway.Offer;

public interface IOfferService
{
    Task<int> CreateSubscriptionAsync(CreateSubscriptionOffer createRequest, string? contactEmail = null);
    Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId);
    Task<OfferDetails?> GetOfferByIdAsync(int offerId);

    /// <summary>
    /// Among the supplied accounts, returns those holding an active subscription to the given offer code.
    /// </summary>
    /// <param name="accountIds">The account identifiers to test.</param>
    /// <param name="offerCode">The offer code.</param>
    /// <returns>The matching account identifiers, or <c>null</c> when the Offer service is unreachable.</returns>
    Task<int[]?> GetAccountIdsWithActiveSubscriptionAsync(int[] accountIds, string offerCode);
}
