using ApiGateway.Offer.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Pennylane;

public class PennylaneService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PennylaneService> logger) : IPennylaneService
{
    public bool ShouldCreateCompanyForOffer(int offerId)
    {
        var pennylaneOfferIdConfig = configuration["PennylaneOfferId"];
        bool shouldCreateCompany = !string.IsNullOrEmpty(pennylaneOfferIdConfig)
            && int.TryParse(pennylaneOfferIdConfig, out int pennylaneOfferId)
            && offerId == pennylaneOfferId;

        logger.LogInformation("OfferId {OfferId} - Company creation will be {Action}.",
            offerId, shouldCreateCompany ? "performed" : "skipped");

        return shouldCreateCompany;
    }

    public async Task<CreateCompanyResult> CreateCompanyAsync(CreateCompanyRequest request)
    {
        var pennylaneClient = httpClientFactory.CreateClient("PennylaneClient");
        var url = "/api/pennylane/companies/onboarding";

        logger.LogDebug("Calling Pennylane API: POST {Url} with AccountId: {AccountId}, Contacts: {ContactCount}",
            url, request.AccountId, request.Contacts.Count);

        var response = await pennylaneClient.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Pennylane company creation failed. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, errorContent);
            throw new HttpRequestException(
                $"POST {url} returned {response.StatusCode}. Response: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateCompanyResult>();

        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize CreateCompanyResult from Pennylane API");
        }

        return result;
    }
}
