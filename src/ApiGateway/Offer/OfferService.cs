using ApiGateway.Models;
using ApiGateway.Offer.Model;

namespace ApiGateway.Offer;

public class OfferService(HttpClient httpClient) : IOfferService
{
    // ids passes en query repetee : ~470 tiennent dans les 8 Ko de request line de Kestrel,
    // on plafonne bien en dessous. Depasser = erreur explicite, pas une URL tronquee.
    private const int MaxAccountIdsPerCall = 200;

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

    public async Task<int[]?> GetAccountIdsWithActiveSubscriptionAsync(int[] accountIds, string offerCode)
    {
        if (accountIds is null || accountIds.Length == 0)
        {
            return [];
        }

        if (accountIds.Length > MaxAccountIdsPerCall)
        {
            throw new ArgumentException(
                $"Maximum {MaxAccountIdsPerCall} accountIds par appel, reçu {accountIds.Length}.",
                nameof(accountIds));
        }

        // ponytail: un seul appel plafonné à MaxAccountIdsPerCall ; découper en lots et concaténer
        // les réponses le jour où un appelant dépasse (GET avec body interdit par la RFC 9110).
        var accountIdParams = string.Join("&", accountIds.Select(accountId => $"accountId={accountId}"));
        var url = $"api/subscription/active-account-ids?offerCode={Uri.EscapeDataString(offerCode)}&{accountIdParams}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<int[]>() ?? [];
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
