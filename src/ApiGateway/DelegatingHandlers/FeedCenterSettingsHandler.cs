using ApiGateway.Authorization;
using ApiGateway.Constants;
using ApiGateway.Extensions;

namespace ApiGateway.DelegatingHandlers
{
    public class FeedCenterSettingsHandler : DelegatingHandler
    {
        private readonly IAuthorizationService _authorizationService;        
        public FeedCenterSettingsHandler(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var isValidContactId = int.TryParse(request.GetQueryParam("contactId"), out var contactId);
            if (isValidContactId) {
                var AllContactAuthorization = await _authorizationService.GetAllContactAuthorizationsAsync(contactId);
                var combinedAuthorization = string.Join(",", AllContactAuthorization); 
                request.Headers.Add(GlobalsConstants.UserPermissionsHeader, combinedAuthorization);
            }
            return await base.SendAsync(request,
                cancellationToken);
        }
    }
}