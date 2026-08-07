using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Helpers;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Middleware;
using Ocelot.Requester;
using Ocelot.Responses;
using System.Net;

namespace ApiGateway.Requester;

/// <summary>
/// Prevents Ocelot from invoking the Prospect wallet-info HTTP pipeline when the Prospect experience is disabled.
/// </summary>
public sealed class WalletInfoProspectFeatureFlagRequester : IHttpRequester
{
    private const string ProspectRouteKey = "WalletInfoProspect";
    private readonly IHttpRequester _innerRequester;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<WalletInfoProspectFeatureFlagRequester> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WalletInfoProspectFeatureFlagRequester"/> class.
    /// </summary>
    /// <param name="innerRequester">The standard Ocelot HTTP requester.</param>
    /// <param name="featureFlagService">The feature flag service.</param>
    /// <param name="logger">The logger.</param>
    public WalletInfoProspectFeatureFlagRequester(
        MessageInvokerHttpRequester innerRequester,
        IFeatureFlagService featureFlagService,
        ILogger<WalletInfoProspectFeatureFlagRequester> logger)
        : this((IHttpRequester)innerRequester, featureFlagService, logger)
    {
    }

    /// <summary>
    /// Initializes a requester with an explicit inner requester for focused testing.
    /// </summary>
    /// <param name="innerRequester">The requester to invoke when the downstream call is allowed.</param>
    /// <param name="featureFlagService">The feature flag service.</param>
    /// <param name="logger">The logger.</param>
    internal WalletInfoProspectFeatureFlagRequester(
        IHttpRequester innerRequester,
        IFeatureFlagService featureFlagService,
        ILogger<WalletInfoProspectFeatureFlagRequester> logger)
    {
        _innerRequester = innerRequester;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the downstream response, or returns a neutral Prospect count without invoking the HTTP requester when disabled.
    /// </summary>
    /// <param name="httpContext">The Ocelot downstream route context.</param>
    /// <returns>The downstream or in-memory HTTP response.</returns>
    public async Task<Response<HttpResponseMessage>> GetResponse(HttpContext httpContext)
    {
        var downstreamRoute = httpContext.Items.DownstreamRoute();
        if (!string.Equals(downstreamRoute?.Key, ProspectRouteKey, StringComparison.OrdinalIgnoreCase))
        {
            return await _innerRequester.GetResponse(httpContext);
        }

        var isProspectExperienceEnabled = await _featureFlagService.IsEnabledAsync(
            FeatureFlagKeys.IsProspectExperienceEnabled,
            defaultValue: false,
            BuildFeatureContext(httpContext.Request),
            httpContext.RequestAborted);

        if (isProspectExperienceEnabled)
        {
            return await _innerRequester.GetResponse(httpContext);
        }

        _logger.LogInformation(
            "Prospect wallet-info dependency skipped because feature flag {FeatureFlagKey} is disabled",
            FeatureFlagKeys.IsProspectExperienceEnabled);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { prospectEntitiesCount = 0 })
        };

        return new OkResponse<HttpResponseMessage>(response);
    }

    /// <summary>
    /// Builds the feature targeting context from the authenticated bearer token.
    /// </summary>
    /// <param name="request">The upstream HTTP request.</param>
    /// <returns>The feature context when an email claim can be read; otherwise, null.</returns>
    private FeatureContext? BuildFeatureContext(HttpRequest request)
    {
        var token = JwtHelper.ExtractBearerToken(request);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return FeatureContext.FromEmail(JwtHelper.ExtractUserEmailFromToken(token));
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            _logger.LogWarning(
                "Unable to read the bearer token; feature flag {FeatureFlagKey} is evaluated without targeting",
                FeatureFlagKeys.IsProspectExperienceEnabled);
            return null;
        }
    }
}
