using ApiGateway.Models;
using ApiGateway.Offer.Model;

namespace ApiGateway.Offer;

public interface IOfferService
{
    Task<int> CreateSubscriptionAsync(CreateSubscriptionOffer createRequest, string? contactEmail = null);
    Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId);
    Task<OfferDetails?> GetOfferByIdAsync(int offerId);
}
