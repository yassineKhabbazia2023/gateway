using ApiGateway.Models;
using ApiGateway.Offer.Model;

namespace ApiGateway.Offer;

public class OfferService(HttpClient httpClient) : IOfferService
{
    public async Task<int> CreateSubscriptionAsync(CreateSubscriptionOffer createRequest, string? contactEmail = null)
    {
        var url = "api/subscription";
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(createRequest) };
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            // ContactEmail header expected by the Offer API for feature flag targeting (Sérénité plan guard)
            request.Headers.Add("ContactEmail", contactEmail);
        }

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST {url} returned {response.StatusCode}");
        }

        var subscriptionId = await response.Content.ReadFromJsonAsync<int>();
        return subscriptionId;
    }

    public async Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId)
    {
        var url = $"api/subscription/status?accountId={accountId}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<SubscriptionStatus[]>() ?? [];
    }

    public async Task<OfferDetails?> GetOfferByIdAsync(int offerId)
    {
        var url = $"api/offers/{offerId}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<OfferDetails>();
    }
}
