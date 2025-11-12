using ApiGateway.Models;
using ApiGateway.Offer.Model;

namespace ApiGateway.Offer;

public class OfferService(HttpClient httpClient) : IOfferService
{
    public async Task<int> CreateSubscriptionAsync(CreateSubscriptionOffer createRequest)
    {
        var url = "/api/subscription";
        var response = await httpClient.PostAsJsonAsync(url, createRequest);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST {url} returned {response.StatusCode}");
        }

        var subscriptionId = await response.Content.ReadFromJsonAsync<int>();
        return subscriptionId;
    }

    public async Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId)
    {
        var url = $"/api/subscription/status?accountId={accountId}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SubscriptionStatus[]>() ?? [];
    }
}
