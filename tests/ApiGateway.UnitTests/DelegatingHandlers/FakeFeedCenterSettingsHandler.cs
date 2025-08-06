
using ApiGateway.Authorization;
using ApiGateway.DelegatingHandlers;

namespace ApiGateway.UnitTests.DelegatingHandlers
{
    public class FakeFeedCenterSettingsHandler : FeedCenterSettingsHandler
    {
        private readonly HttpMessageHandler _httpMessageHandler;

        public FakeFeedCenterSettingsHandler(HttpMessageHandler httpMessageHandler, IAuthorizationService authorizationService)
            :base(authorizationService)
        {
            _httpMessageHandler = httpMessageHandler;
        }

        public async Task<HttpResponseMessage> FakeSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InnerHandler = _httpMessageHandler;
            return await SendAsync(request, cancellationToken);
        }
    }
}