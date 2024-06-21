// Global using directives

using ApiGateway.Aggregator.Models;
using Newtonsoft.Json;
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
            var responsesDownstream = responses.Select(x => x.Items.DownstreamResponse()).ToArray();

            var result = new List<Aggregator.Models.Configuration>();
            foreach (var response in responsesDownstream)
            {
                var content = await response.Content.ReadAsStringAsync();
                var configurations = JsonConvert.DeserializeObject<List<Aggregator.Models.Configuration>>(content);
                Merge(configurations!, result);
            }

            return new DownstreamResponse(
                new StringContent(JsonConvert.SerializeObject(result), new MediaTypeHeaderValue("application/json")),
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
                    l.AddRange(d.Actions);
                    d.Actions = l;
                }
            }

            // tolowerCase
            foreach(var d in destination)
            {
                d.Category = d.Category.ToLowerInvariant();
            }
        }
    }
}
