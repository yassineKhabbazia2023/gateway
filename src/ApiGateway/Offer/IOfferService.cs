using ApiGateway.Models;

namespace ApiGateway.Offer;

public interface IOfferService
{
    Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId);
}
