using ApiGateway.Models;

namespace ApiGateway.Offer;

public class OfferService(HttpClient httpClient) : IOfferService
{
    public async Task<SubscriptionStatus[]?> GetSubscriptionsAsync(int accountId)
    {
        var url = $"/api/subscription/status?accountId={accountId}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SubscriptionStatus[]>() ?? [];
    }
}
