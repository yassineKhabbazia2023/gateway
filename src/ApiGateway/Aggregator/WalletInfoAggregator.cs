using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ApiGateway.Aggregator
{
    public class WalletInfoAggregator : IDefinedAggregator
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
            var prospectEntry = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, ProspectRouteKey, StringComparison.OrdinalIgnoreCase));

            var accountResponse = accountEntry?.Response;
            var prospectResponse = prospectEntry?.Response;

            if (accountResponse == null || prospectResponse == null)
            {
                return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
            }

            if (accountResponse.StatusCode == HttpStatusCode.Forbidden || prospectResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                return BuildErrorResponse(HttpStatusCode.Forbidden, responsesDownstream.Select(x => x.Response!));
            }

            var regularEntitiesCount = await ExtractCountAsync(accountResponse.Content, "regularEntitiesCount");
            var prospectEntitiesCount = await ExtractCountAsync(prospectResponse.Content, "prospectEntitiesCount");

            var result = new Dictionary<string, int>
            {
                ["regularEntitiesCount"] = regularEntitiesCount,
                ["prospectEntitiesCount"] = prospectEntitiesCount
            };

            return new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(result), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                responsesDownstream.SelectMany(x => x.Response!.Headers).ToList(),
                "OK");
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
