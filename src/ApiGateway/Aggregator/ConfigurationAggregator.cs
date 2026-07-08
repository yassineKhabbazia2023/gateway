// Global using directives

using ApiGateway.Aggregator.Models;
using ApiGateway.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;

namespace ApiGateway.Aggregator
{
    public class ConfigurationAggregator : IDefinedAggregator
    {
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var responsesDownstream = responses.Select(x => x.Items.DownstreamResponse()).Where(x => x != null).ToArray();

            if (responses.Any(r => r.Response.StatusCode == (int)HttpStatusCode.Forbidden))
            {
                return new DownstreamResponse(null, HttpStatusCode.Forbidden, responsesDownstream.SelectMany(x => x.Headers).ToList(), "reason");
            }

            var result = new List<Models.Configuration>();
            foreach (var response in responsesDownstream)
            {
                // Skip 204 No Content and 404 Not Found (no data available, expected behavior)
                if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                var configurations = JsonConvert.DeserializeObject<List<Models.Configuration>>(content);
                Merge(configurations!, result);
            }

            return new DownstreamResponse(
                new StringContent(JsonConvert.SerializeObject(result, GlobalsConstants.JsonSerializerSettings), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                responsesDownstream.SelectMany(x => x.Headers).ToList(),
                "reason");
        }

        private void Merge(List<Models.Configuration> src, List<Models.Configuration> destination)
        {
            foreach (var s in src)
            {
                var d = destination.FirstOrDefault(x => x.Category == s.Category);
                if (d == null)
                {
                    destination.Add(s);
                }
                else
                {
                    var l = d.Actions.ToList();
                    l.AddRange(s.Actions);
                    d.Actions = l;
                }
            }
        }
    }
}
