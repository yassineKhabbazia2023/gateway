// Global using directives

using Ocelot.Middleware;

namespace ApiGateway.Aggregrator.Interface
{
    public interface IDefinedAggregator
    {
        Task<DownstreamResponse> Aggregate(List<HttpContext> responses);
    }
}
