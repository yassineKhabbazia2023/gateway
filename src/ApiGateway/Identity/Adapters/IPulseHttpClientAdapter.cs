using System.Net.Http;

namespace ApiGateway.Identity.Adapters
{
    public interface IPulseHttpClientAdapter
    {
        Task<string> GetStringAsync(Uri requestUri);
    }
}
