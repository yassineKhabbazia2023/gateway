using System.Net;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Helpers;

namespace ApiGateway.DelegatingHandlers;

/// <summary>
/// Blocks QA-sensitive endpoints when the dedicated feature flag is disabled.
/// </summary>
/// <param name="featureFlagService">The feature flag service.</param>
/// <param name="logger">The logger.</param>
public sealed class QaSensitiveEndpointsHandler(
    IFeatureFlagService featureFlagService,
    ILogger<QaSensitiveEndpointsHandler> logger) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = JwtHelper.ExtractBearerToken(request);
        var userEmail = string.IsNullOrEmpty(token) ? null : JwtHelper.ExtractUserEmailFromToken(token);
        var context = FeatureContext.FromEmail(userEmail);

        if (await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsQaSensitiveEndpointsEnabled, context: context, ct: cancellationToken))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogWarning("[Response]: 403 - [Handler]: QaSensitiveEndpointsHandler - [Function]: SendAsync - [Reason]: QA sensitive endpoints are disabled by feature flag");
        return new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new
            {
                ErrorCode = Errors.FeatureFlagDisabledCode,
                ErrorMessage = string.Format(Errors.FeatureFlagDisabledMessage, FeatureFlagKeys.IsQaSensitiveEndpointsEnabled)
            })
        };
    }
}
