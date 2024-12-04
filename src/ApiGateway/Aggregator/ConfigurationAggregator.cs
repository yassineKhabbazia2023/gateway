// Global using directives

using ApiGateway.Aggregator.Models;
using ApiGateway.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;

namespace ApiGateway.Aggregrator
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

            var result = new List<Aggregator.Models.Configuration>();
            foreach (var response in responsesDownstream)
            {
                var content = await response.Content.ReadAsStringAsync();
                var configurations = JsonConvert.DeserializeObject<List<Aggregator.Models.Configuration>>(content);
                Merge(configurations!, result);
            }

            return new DownstreamResponse(
                new StringContent(JsonConvert.SerializeObject(result, GlobalsConstants.JsonSerializerSettings), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                responsesDownstream.SelectMany(x => x.Headers).ToList(),
                "reason");
        }

        private void Merge(List<Aggregator.Models.Configuration> src, List<Aggregator.Models.Configuration> destination)
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
