// Global using directives

using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ApiGateway.Aggregator
{
    public class SubmissionAggregator : IDefinedAggregator
    {
        private const string RegistryRouteKey = "HubspotSubmissionRegistry";
        private const string AccountRouteKey = "VentyaDematReady";

        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var responsesDownstream = responses
                .Select(x => new
                {
                    Context = x,
                    Route = x.Items.DownstreamRoute(),
                    Response = x.Items.DownstreamResponse()
                })
                .Where(x => x.Response != null)
                .ToArray();

            var responseA = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, RegistryRouteKey, StringComparison.OrdinalIgnoreCase))
                ?.Response;
            var responseB = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, AccountRouteKey, StringComparison.OrdinalIgnoreCase))
                ?.Response;

            if (responseA == null || responseB == null)
            {
                return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
            }

            if (responseA.StatusCode == HttpStatusCode.NotFound)
            {
                return new DownstreamResponse(
                    new StringContent(string.Empty),
                    HttpStatusCode.NotFound,
                    responseA.Headers.ToList(),
                    "Not Found");
            }

            if (responseA.StatusCode == HttpStatusCode.OK)
            {
                if (responseB.StatusCode != HttpStatusCode.OK)
                {
                    return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
                }

                var isReady = await TryReadIsReadyAsync(responseB.Content);
                if (!isReady.HasValue)
                {
                    return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
                }

                return BuildStatusResponse(isReady.Value ? "completed" : "submitted", responsesDownstream.Select(x => x.Response!));
            }

            return responseA;
        }

        private static async Task<bool?> TryReadIsReadyAsync(HttpContent content)
        {
            var payload = await content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "isReady", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => bool.TryParse(property.Value.GetString(), out var parsed) ? parsed : null,
                        _ => null
                    };
                }

                return null;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private static DownstreamResponse BuildStatusResponse(string status, IEnumerable<DownstreamResponse> responses)
        {
            return new DownstreamResponse(
                new StringContent(status, new MediaTypeHeaderValue("text/plain")),
                HttpStatusCode.OK,
                responses.SelectMany(x => x.Headers).ToList(),
                "OK");
        }

        private static DownstreamResponse BuildErrorResponse(HttpStatusCode statusCode, IEnumerable<DownstreamResponse> responses)
        {
            return new DownstreamResponse(
                new StringContent(string.Empty),
                statusCode,
                responses.SelectMany(x => x.Headers).ToList(),
                statusCode == HttpStatusCode.InternalServerError ? "Internal Server Error" : statusCode.ToString());
        }
    }
}
