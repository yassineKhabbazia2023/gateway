using ApiGateway.Offer.Model;
using ApiGateway.Pennylane.Models;
using IdentityModel.OidcClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
            logger.LogError("Failed to deserialize CreateCompanyResult for AccountId: {AccountId}", request.AccountId);
            throw new InvalidOperationException("Failed to deserialize CreateCompanyResult from Pennylane API");
        }

        return result;
    }

    public async Task<GrantPennylaneAccessResult> GrantPennylaneAccessAsync(PennylaneAuthorizationRequest request)
    {
        var pennylaneClient = httpClientFactory.CreateClient("PennylaneClient");
        var url = "/api/pennylane/role/create";

        try
        {
            logger.LogInformation("Calling Pennylane API: POST {Url} for ContactId: {ContactId}, AccountId: {AccountId}, Role: {Role}",
                url, request.ContactId, request.AccountId, request.Role);

            var response = await pennylaneClient.PostAsJsonAsync(url, request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Pennylane access grant failed. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException(
                    $"POST {url} returned {response.StatusCode}. Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            try
            {
                var result = JsonSerializer.Deserialize<GrantPennylaneAccessResult>(content);

                if (result == null)
                {
                    logger.LogError("Failed to deserialize GrantPennylaneAccessResult for ContactId: {ContactId}, AccountId: {AccountId}. Raw response: {Response}",
                        request.ContactId, request.AccountId, content);
                    throw new InvalidOperationException("Failed to deserialize GrantPennylaneAccessResult from Pennylane API");
                }

                logger.LogInformation("Pennylane access grant response status {Status} for ContactId: {ContactId}, AccountId: {AccountId}",
                    result.Status, request.ContactId, request.AccountId);

                return result;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize GrantPennylaneAccessResult for ContactId: {ContactId}, AccountId: {AccountId}. Raw response: {Response}",
                    request.ContactId, request.AccountId, content);
                throw new InvalidOperationException("Failed to deserialize GrantPennylaneAccessResult from Pennylane API", ex);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Pennylane access grant HTTP request failed for ContactId: {ContactId}, AccountId: {AccountId}",
                request.ContactId, request.AccountId);
            throw;
        }
    }

    public async Task<GrantPennylaneAccessResult> UpdatePennylaneRoleAsync(PennylaneAuthorizationRequest request)
    {
        var pennylaneClient = httpClientFactory.CreateClient("PennylaneClient");
        var url = "/api/pennylane/role";


        logger.LogInformation("Updating Pennylane role via POST {Url} for ContactId: {ContactId}, AccountId: {AccountId}, Role: {Role}",
            url, request.ContactId, request.AccountId, request.Role);

        var response = await pennylaneClient.PostAsJsonAsync(url, request);

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Pennylane role update failed. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, content);
            throw new PennylaneApiException(response.StatusCode, content, url);
        }

        logger.LogInformation("Pennylane role update returned status {StatusCode} for ContactId: {ContactId}, AccountId: {AccountId}. Response: {Response}",
            response.StatusCode, request.ContactId, request.AccountId, content);

        return new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.ExistingUserAccessGranted,
            Message = string.IsNullOrWhiteSpace(content) ? "Role updated" : content,
            ContactId = request.ContactId,
            AccountId = request.AccountId,
            Role = request.Role
        };

    }

    public async Task<RevokePennylaneAccessResult> RevokePennylaneAccessAsync(RevokeAccessRequest request)
    {
        var pennylaneClient = httpClientFactory.CreateClient("PennylaneClient");
        var url = "/api/pennylane/role/revoke";

        logger.LogInformation("Revoking Pennylane access via POST {Url} for ContactId: {ContactId}, AccountId: {AccountId}",
            url, request.ContactId, request.AccountId);

        var response = await pennylaneClient.PostAsJsonAsync(url, request);

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Pennylane access revoke failed. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, content);
            throw new HttpRequestException(
                $"POST {url} returned {response.StatusCode}. Response: {content}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<RevokePennylaneAccessResult>(content);

            if (result == null)
            {
                logger.LogError("Failed to deserialize RevokePennylaneAccessResult for ContactId: {ContactId}, AccountId: {AccountId}. Raw response: {Response}",
                    request.ContactId, request.AccountId, content);
                throw new InvalidOperationException("Failed to deserialize RevokePennylaneAccessResult from Pennylane API");
            }

            logger.LogInformation("Pennylane access revoke response status {Status} for ContactId: {ContactId}, AccountId: {AccountId}",
                result.Status, request.ContactId, request.AccountId);

            return result;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize RevokePennylaneAccessResult for ContactId: {ContactId}, AccountId: {AccountId}. Raw response: {Response}",
                request.ContactId, request.AccountId, content);
            throw new InvalidOperationException("Failed to deserialize RevokePennylaneAccessResult from Pennylane API", ex);
        }
    }
}
