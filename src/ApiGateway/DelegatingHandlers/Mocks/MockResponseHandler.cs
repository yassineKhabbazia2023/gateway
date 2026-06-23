using System.Net;
using System.Text;
using ApiGateway.FeatureFlags;
using Microsoft.Extensions.Logging;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseHandler(
    IMockResponseRepository responseRepository,
    IFeatureFlagService featureFlagService,
    ILogger<MockResponseHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, ct: cancellationToken))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (request.RequestUri == null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var pathAndQuery = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri.PathAndQuery
            : request.RequestUri.OriginalString;
        var routeKey = $"{request.Method}:{pathAndQuery}".ToLower();

        logger.LogDebug("MockResponseHandler - RouteKey: {RouteKey} | FullUri: {FullUri}", routeKey, request.RequestUri);

        var (success, jsonContent) = await responseRepository.GetJsonContentAsync(routeKey);
        if (!success)
        {
            logger.LogDebug("MockResponseHandler - No mock found for: {RouteKey}", routeKey);
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogDebug("MockResponseHandler - Mock matched for: {RouteKey}", routeKey);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonContent!,
                Encoding.UTF8,
                "application/json")
        };
    }
}
