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
            var responsesDownstream = responses.Select(x => x.Items.DownstreamResponse()).ToArray();

            var result = new List<Aggregator.Models.Configuration>();
            foreach (var response in responsesDownstream)
            {
                var content = await response.Content.ReadAsStringAsync();
                var configurations = JsonConvert.DeserializeObject<List<Aggregator.Models.Configuration>>(content);
                result = Merge(configurations!, result);
            }

            return new DownstreamResponse(
                new StringContent(JsonConvert.SerializeObject(result, GlobalsConstants.JsonSerializerSettings), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                responsesDownstream.SelectMany(x => x.Headers).ToList(),
                "reason");
        }

        private List<Aggregator.Models.Configuration> Merge(List<Aggregator.Models.Configuration> src, List<Aggregator.Models.Configuration> destination)
        {
            var merged = destination;
            foreach (var s in src)
            {
                var destCategory = destination.FirstOrDefault(x => x.Category == s.Category);
                if (destCategory == null)
                {
                    destCategory = s;
                }
                else
                {
                    var actionsList = destCategory.Actions?.ToList() ?? new List<Aggregator.Models.Action>();
                    actionsList.AddRange(s.Actions);
                    destCategory.Actions = actionsList;
                }
                merged.Add(destCategory);
            }

            return merged;
        }
    }
}
