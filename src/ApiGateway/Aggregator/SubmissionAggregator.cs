using ApiGateway.Helpers;
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

            var entryA = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, RegistryRouteKey, StringComparison.OrdinalIgnoreCase));
            var entryB = responsesDownstream
                .FirstOrDefault(x => string.Equals(x.Route?.Key, AccountRouteKey, StringComparison.OrdinalIgnoreCase));

            var responseA = entryA?.Response;
            var responseB = entryB?.Response;

            if (responseA == null || responseB == null)
            {
                return BuildErrorResponse(HttpStatusCode.InternalServerError, responsesDownstream.Select(x => x.Response!));
            }

            // B = 404 → account inexistant
            if (responseB.StatusCode == HttpStatusCode.NotFound)
            {
                return new DownstreamResponse(
                    new StringContent(string.Empty),
                    HttpStatusCode.NotFound,
                    responseB.Headers.ToList(),
                    "Not Found");
            }

            // Extraire les données de B (contactWithAccess, externalDematMail, isReady)
            var accountData = await TryReadAccountResponseAsync(responseB.Content);
            var contactWithAccess = accountData?.ContactWithAccess;
            var externalDematMail = accountData?.ExternalDematMail;

            // Si B = 200 OK et isReady = true
            if (responseB.StatusCode == HttpStatusCode.OK && accountData?.IsReady == true)
            {
                var currentUserEmail = ExtractCurrentUserEmail(entryA!.Context);

                var tabState = string.Equals(currentUserEmail, contactWithAccess, StringComparison.OrdinalIgnoreCase)
                    ? "connection-ready-unlocked"
                    : "connection-ready-locked";

                return BuildJsonResponse(tabState, contactWithAccess, externalDematMail, responsesDownstream.Select(x => x.Response!));
            }

            // Sinon → vérifier A
            var tabStateFromRegistry = responseA.StatusCode == HttpStatusCode.NotFound
                ? "form-start"
                : "form-submitted";

            return BuildJsonResponse(tabStateFromRegistry, contactWithAccess, externalDematMail, responsesDownstream.Select(x => x.Response!));
        }

        private static string ExtractCurrentUserEmail(HttpContext httpContext)
        {
            var token = JwtHelper.ExtractBearerToken(httpContext.Request);
            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }
            return JwtHelper.ExtractUserEmailFromToken(token);
        }

        private static async Task<AccountResponse?> TryReadAccountResponseAsync(HttpContent content)
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

                bool? isReady = null;
                string? contactWithAccess = null;
                string? externalDematMail = null;

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "isReady", StringComparison.OrdinalIgnoreCase))
                    {
                        isReady = property.Value.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.String => bool.TryParse(property.Value.GetString(), out var parsed) ? parsed : null,
                            _ => null
                        };
                    }
                    else if (string.Equals(property.Name, "contactWithAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        contactWithAccess = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : null;
                    }
                    else if (string.Equals(property.Name, "externalDematMail", StringComparison.OrdinalIgnoreCase))
                    {
                        externalDematMail = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : null;
                    }
                }

                return new AccountResponse(isReady, contactWithAccess, externalDematMail);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static DownstreamResponse BuildJsonResponse(
            string tabState,
            string? contactWithAccess,
            string? externalDematMail,
            IEnumerable<DownstreamResponse> responses)
        {
            var result = new Dictionary<string, string?>
            {
                ["tab-state"] = tabState,
                ["contactWithAccess"] = contactWithAccess,
                ["externalDematMail"] = externalDematMail
            };

            var json = JsonSerializer.Serialize(result);

            return new DownstreamResponse(
                new StringContent(json, new MediaTypeHeaderValue("application/json")),
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

        private sealed record AccountResponse(bool? IsReady, string? ContactWithAccess, string? ExternalDematMail);
    }
}
