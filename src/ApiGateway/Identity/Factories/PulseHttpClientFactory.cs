
using ApiGateway.Identity.Adapters;

namespace ApiGateway.Identity.Factories
{
    public class PulseHttpClientFactory : IPulseHttpClientFactory
    {

        public IPulseHttpClientAdapter CreateClient()
        {
            return new PulseHttpClientAdapter(new HttpClient());
        }
    }
}
