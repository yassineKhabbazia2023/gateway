using ApiGateway.FeatureFlags;
using ApiGateway.Helpers;
using ApiGateway.Offer.Constants;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiGateway.DelegatingHandlers;

public class ApprovedPlatformFilterHandler(IFeatureFlagService featureFlagService) : DelegatingHandler
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

        if (await featureFlagService.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, userEmail, cancellationToken))
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
}
