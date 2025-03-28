using ApiGateway.Exceptions;
using ApiGateway.Identity.Exceptions;
using ApiGateway.Identity.Models;
using ApiGateway.Identity.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ApiGateway.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly HttpClient httpClient;
        private readonly IOptions<IdentityServiceOptions> options;
        private readonly ILogger<IdentityService> logger;

        public IdentityService(HttpClient httpClient, IOptions<IdentityServiceOptions> options, ILogger<IdentityService> logger)
        {
            this.httpClient = httpClient;
            this.options = options;
            this.logger = logger;
        }

        public bool ValidateCollaborator(HttpContext httpContext)
        {
            if (string.IsNullOrEmpty(options.Value.CollaboratorsSecurityGroup))
            {
                return true;
            }
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
                                ?? throw new GatewayException(StatusCodes.Status500InternalServerError, Errors.GigyaError, "Failed to deserialize Gigya response.");
                logger.LogInformation($"[Function]: CheckUserExistsInGigyaAsync;  [GIGYA RESPONSE]: {responseContent}");
            }
            catch (JsonException ex)
            {
                throw new GatewayException(StatusCodes.Status500InternalServerError, Errors.GigyaError, "Failed to deserialize Gigya response.");
            }

            var anomaly = GetAnomalyError(gigyaResponse);
            if (anomaly is not null)
            {
                throw new GatewayException(StatusCodes.Status500InternalServerError, Errors.GigyaError, $"Something went wrong while communicating with Gigya, details: {anomaly}");
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
