
using ApiGateway.Authorization;

namespace ApiGateway.UnitTests.DelegatingHandlers
{
    public class FakeFeedCenterSettingsHandler : FeedCenterSettingsHandler
    {
        private readonly HttpMessageHandler _httpMessageHandler;

        public FakeFeedCenterSettingsHandler(HttpMessageHandler httpMessageHandler, IAuthorizationSevice authorizationSevice)
            :base(authorizationSevice)
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