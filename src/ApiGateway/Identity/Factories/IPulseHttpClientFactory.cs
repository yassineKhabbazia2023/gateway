using ApiGateway.Identity.Adapters;

namespace ApiGateway.Identity.Factories
{
    public interface IPulseHttpClientFactory
    {
        IPulseHttpClientAdapter CreateClient();
    }
}
