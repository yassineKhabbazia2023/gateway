using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Helpers;
using System.Net;

namespace ApiGateway.DelegatingHandlers;

public class ProspectExperienceHandler(IFeatureFlagService featureFlagService, ILogger<ProspectExperienceHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = JwtHelper.ExtractBearerToken(request);
        var userEmail = string.IsNullOrEmpty(token) ? null : JwtHelper.ExtractUserEmailFromToken(token);

        if (await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, userEmail, cancellationToken))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogWarning("[Response]: 403 - [Handler]: ProspectExperienceHandler - [Function]: SendAsync - [Reason]: Prospect experience is disabled by feature flag");
        return new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new { ErrorCode = Errors.ProspectExperienceDisabledCode, ErrorMessage = Errors.ProspectExperienceDisabledMessage })
        };
    }
}
