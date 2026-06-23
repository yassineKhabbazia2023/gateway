using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using Ocelot.Metadata;
using Ocelot.Middleware;
using System.Net;

namespace ApiGateway.DelegatingHandlers;

public class FeatureFlagGateHandler(
    IFeatureFlagService featureFlagService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FeatureFlagGateHandler> logger) : DelegatingHandler
{
    internal const string MetadataKey = "featureFlag";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var flagKey = httpContext?.Items.DownstreamRoute()?.GetMetadata<string>(MetadataKey);

        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var context = BuildFeatureContext(request);
        var isEnabled = await featureFlagService.IsEnabledAsync(flagKey, defaultValue: false, context, cancellationToken);

        if (isEnabled)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogWarning(
            "[Response]: 403 - [Handler]: FeatureFlagGateHandler - [Function]: SendAsync - [Reason]: Feature flag '{FlagKey}' is disabled for path '{Path}'",
            flagKey, httpContext!.Request.Path.Value);

        return new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new
            {
                ErrorCode = Errors.FeatureFlagDisabledCode,
                ErrorMessage = string.Format(Errors.FeatureFlagDisabledMessage, flagKey)
            })
        };
    }

    private static FeatureContext? BuildFeatureContext(HttpRequestMessage request)
    {
        var contactEmail = request.Headers.TryGetValues("ContactEmail", out var emailValues)
            ? emailValues.FirstOrDefault()
            : null;

        return FeatureContext.FromEmail(contactEmail);
    }
}
