
using ApiGateway.Identity.Adapters;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Factories
{
    [ExcludeFromCodeCoverage]
    public class PulseHttpClientFactory : IPulseHttpClientFactory
    {
        public IPulseHttpClientAdapter CreateClient()
        {
            return new PulseHttpClientAdapter(new HttpClient());
        }
    }
}
