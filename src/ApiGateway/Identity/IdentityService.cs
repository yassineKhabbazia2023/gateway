using ApiGateway.Identity.Exceptions;
using ApiGateway.Identity.Models;
using ApiGateway.Identity.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiGateway.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly HttpClient httpClient;
        private readonly IOptions<IdentityServiceOptions> options;

        public IdentityService(HttpClient httpClient, IOptions<IdentityServiceOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options;

        }

        public bool IsCollaborator(HttpContext httpContext)
        {
            return httpContext.User.IsInRole(options.Value.CollaboratorRole);
        }

        public bool IsCustomer(HttpContext httpContext)
        {
            return httpContext.User.IsInRole(options.Value.CustomerRole);
        }

        public bool ValidateCollaborator(HttpContext httpContext)
        {
            var user = httpContext.User;

            var hasRequiredGroup = user.Claims
                .Where(claim => claim.Type == "groups")
                .Any(claim => claim.Value == options.Value.CollaboratorsSecurityGroup);

            return hasRequiredGroup;
        }

        public async Task<bool> ValidateCustomerAsync(string email)
        {
            return await CheckUserExistsInGigyaAsync(email);
        }

        public async Task<bool> CheckUserExistsInGigyaAsync(string email)
        {
            var requestUrl = "accounts.search";
            var query = $"SELECT * FROM accounts WHERE profile.email = '{email}'";

            var values = new Dictionary<string, string>
            {
                { "apiKey", options.Value.GigyaApiKey },
                { "secret", options.Value.GigyaSecret },
                { "userKey", options.Value.GigyaUserKey },
                { "query", query }
            };

            var content = new FormUrlEncodedContent(values);

            HttpResponseMessage response = await httpClient.PostAsync(requestUrl, content);


            var responseContent = await response.Content.ReadAsStringAsync();

            GigyaResponse gigyaResponse;
            try
            {
                gigyaResponse = JsonConvert.DeserializeObject<GigyaResponse>(responseContent)
                                ?? throw new GigyaOperationException("Failed to deserialize Gigya response.");
            }
            catch (JsonException ex)
            {
                throw new GigyaOperationException("Error deserializing Gigya response.", ex);
            }

            var anomaly = GetAnomalyError(gigyaResponse);
            if (anomaly is not null)
            {
                throw new GigyaOperationException($"Something went wrong while communicating with Gigya, details: {anomaly}");
            }

            return gigyaResponse.TotalCount > 0;
        }

        private Anomaly? GetAnomalyError(GigyaResponse gigyaResponse)
        {
            if (gigyaResponse.ErrorCode != 0)
            {
                return new Anomaly(gigyaResponse.ErrorCode.ToString(), gigyaResponse.ErrorDetails ?? string.Empty);
            }

            return null;
        }
    }
}
