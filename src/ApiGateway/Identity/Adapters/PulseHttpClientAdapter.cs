namespace ApiGateway.Identity.Adapters
{
    public class PulseHttpClientAdapter : IPulseHttpClientAdapter
    {
        private readonly HttpClient httpClient;

        public PulseHttpClientAdapter(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public Task<string> GetStringAsync(Uri requestUri)
        {
            return this.httpClient.GetStringAsync(requestUri);
        }
    }
}
