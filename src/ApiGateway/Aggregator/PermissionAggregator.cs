// Global using directives

using ApiGateway.Aggregator.Models;
using Newtonsoft.Json;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Net.Http.Headers;

namespace ApiGateway.Aggregrator
{
    public class PermissionAggregator : IDefinedAggregator
    {
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var responsesDownstream = responses.Select(x => x.Items.DownstreamResponse()).ToArray();

            var result = new List<string>();
            foreach (var response in responsesDownstream)
            {
                var content = await response.Content.ReadAsStringAsync();
                var permissions = JsonConvert.DeserializeObject<List<string>>(content);
                result.AddRange(permissions!);
            }

            return new DownstreamResponse(
                new StringContent(JsonConvert.SerializeObject(result), new MediaTypeHeaderValue("application/json")),
                HttpStatusCode.OK,
                responsesDownstream.SelectMany(x => x.Headers).ToList(),
                "reason");
        }
    }
}
