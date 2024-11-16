using ApiGateway.Identity.Adapters;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Factories
{
    public interface IPulseHttpClientFactory
    {
        IPulseHttpClientAdapter CreateClient();
    }
}
