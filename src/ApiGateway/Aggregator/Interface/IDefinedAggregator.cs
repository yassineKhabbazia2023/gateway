// Global using directives

using Ocelot.Middleware;

namespace ApiGateway.Aggregator.Interface
{
    public interface IDefinedAggregator
    {
        Task<DownstreamResponse> Aggregate(List<HttpContext> responses);
    }
}
