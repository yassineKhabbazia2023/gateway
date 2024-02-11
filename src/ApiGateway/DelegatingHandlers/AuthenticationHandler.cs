using System.Net;
using ApiGateway.Security;

namespace ApiGateway.DelegatingHandlers;

public class AuthenticationHandler(IAuthenticationService authenticationService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isAuthorized = authenticationService.IsAllowed(request);
        if (isAuthorized) return await base.SendAsync(request, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized: Access is denied.")
        };
    }
}