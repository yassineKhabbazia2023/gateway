using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Helpers;
using ApiGateway.Offer.Constants;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiGateway.DelegatingHandlers;

public class ApprovedPlatformFilterHandler(IFeatureFlagService featureFlagService, ILogger<ApprovedPlatformFilterHandler> logger) : DelegatingHandler
{
    private const string ExcludePlanCodesParam = "excludePlanCodes";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = JwtHelper.ExtractBearerToken(request);
        var userEmail = string.IsNullOrEmpty(token) ? null : JwtHelper.ExtractUserEmailFromToken(token);

        var context = FeatureContext.FromEmail(userEmail);

        if (await IsApprovedPlatformEnabledAsync(context, cancellationToken))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var updatedUri = QueryHelpers.AddQueryString(
            request.RequestUri.ToString(),
            ExcludePlanCodesParam,
            OfferPlanCodes.ApprovedPlatform);
        request.RequestUri = new Uri(updatedUri);

        return await base.SendAsync(request, cancellationToken);
    }

    // Fail closed : en cas d'erreur d'évaluation, le plan APPROVED_PLATFORM reste masqué
    private async Task<bool> IsApprovedPlatformEnabledAsync(FeatureContext? context, CancellationToken cancellationToken)
    {
        try
        {
            return await featureFlagService.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, context: context, ct: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Feature flag '{FlagKey}' evaluation failed; treating it as disabled", FeatureFlagKeys.EnableApprovedPlatform);
            return false;
        }
    }
}
