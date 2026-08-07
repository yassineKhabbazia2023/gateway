using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Helpers;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ApiGateway.Aggregator
{
   
    public class WalletInfoAggregator(
        IFeatureFlagService featureFlagService,
        ILogger<WalletInfoAggregator> logger) : IDefinedAggregator
    {
        private const string AccountRouteKey = "WalletInfoAccount";
        private const string ProspectRouteKey = "WalletInfoProspect";

        
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var responsesDownstream = responses
                .Select(x => new
                {
                    Route = x.Items.DownstreamRoute(),
                    Response = x.Items.DownstreamResponse()
                })
                .Where(x => x.Response != null)
                .ToArray();

            var accountEntry = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, AccountRouteKey, StringComparison.OrdinalIgnoreCase));

            var accountResponse = accountEntry?.Response;

            if (accountResponse == null)
            {
                return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
            }

            if (accountResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                return BuildErrorResponse(HttpStatusCode.Forbidden, responsesDownstream.Select(x => x.Response!));
            }

            var regularEntitiesCount = await ExtractCountAsync(accountResponse.Content, "regularEntitiesCount");
            var prospectEntitiesCount = 0;


            var isProspectEnabled = await featureFlagService.IsEnabledAsync(
            FeatureFlagKeys.IsProspectExperienceEnabled,
            true,
            BuildFeatureContext(responses));

            if (isProspectEnabled)
            {
                var prospectEntry = responsesDownstream
                    .FirstOrDefault(x => string.Equals(x.Route?.Key, ProspectRouteKey, StringComparison.OrdinalIgnoreCase));

                var prospectResponse = prospectEntry?.Response;

                if (prospectResponse == null)
                {
                    // Flag actif mais la route WalletInfoProspect n'a pas répondu (route supprimée) :
                    // l'endpoint doit tenir quand même, on ne remonte pas d'erreur au client.
                    logger.LogInformation(
                        "[Aggregator]: WalletInfoAggregator - Prospect bypassed because the WalletInfoProspect route did not answer.");
                }
                else if (prospectResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Flag actif : un 403 est un vrai refus d'autorisation, on ne le masque pas.
                    return BuildErrorResponse(HttpStatusCode.Forbidden, responsesDownstream.Select(x => x.Response!));
                }
                else
                {
                    prospectEntitiesCount = await ExtractCountAsync(prospectResponse.Content, "prospectEntitiesCount");
                }
            }
            else
            {
                logger.LogInformation(
                "[Aggregator]: WalletInfoAggregator - Prospect bypassed because the feature flag is disabled.");
            }

            var result = new Dictionary<string, int>
            {
                ["regularEntitiesCount"] = regularEntitiesCount,
                ["prospectEntitiesCount"] = prospectEntitiesCount
            };

            return new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(result), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                accountResponse.Headers.ToList(),
                "OK");
        }

        /// <summary>
        /// Construit le contexte d'évaluation du flag à partir de l'email porté par le JWT upstream,
        /// comme le fait <see cref="DelegatingHandlers.ProspectExperienceHandler"/> : les deux évaluations
        /// du même flag sur une même requête doivent utiliser la même source de targeting.
        /// </summary>
        private FeatureContext? BuildFeatureContext(List<HttpContext> responses)
        {
            var request = responses.FirstOrDefault()?.Request;
            if (request == null)
            {
                return null;
            }

            var token = JwtHelper.ExtractBearerToken(request);
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            try
            {
                return FeatureContext.FromEmail(JwtHelper.ExtractUserEmailFromToken(token));
            }
            catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
            {
                logger.LogWarning(
                    "[Aggregator]: WalletInfoAggregator - [Function]: BuildFeatureContext - [Reason]: Unable to read the bearer token, the feature flag is evaluated without targeting");
                return null;
            }
        }

        private static async Task<int> ExtractCountAsync(HttpContent content, string propertyName)
        {
            var payload = await content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return 0;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty(propertyName, out var property)
                    && property.TryGetInt32(out var value))
                {
                    return value;
                }
            }
            catch (JsonException)
            {
            }

            return 0;
        }

        private static DownstreamResponse BuildErrorResponse(HttpStatusCode statusCode, IEnumerable<DownstreamResponse> responses)
        {
            return new DownstreamResponse(
                new StringContent(string.Empty),
                statusCode,
                responses.SelectMany(x => x.Headers).ToList(),
                statusCode.ToString());
        }
    }
}
