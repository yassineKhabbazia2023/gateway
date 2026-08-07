using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Requester;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Ocelot.Configuration;
using Ocelot.Configuration.File;
using Ocelot.Middleware;
using Ocelot.Requester;
using Ocelot.Responses;
using Ocelot.Values;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace ApiGateway.UnitTests.Requester;

/// <summary>
/// Tests the transport-level Prospect wallet-info feature flag guard.
/// </summary>
public class WalletInfoProspectFeatureFlagRequesterTests
{
    private const string AccountRouteKey = "WalletInfoAccount";
    private const string ProspectRouteKey = "WalletInfoProspect";
    private readonly Mock<IHttpRequester> _innerRequester = new(MockBehavior.Strict);
    private readonly Mock<IFeatureFlagService> _featureFlagService = new(MockBehavior.Strict);

    #region GetResponse

    /// <summary>
    /// Verifies that a disabled Prospect experience returns a neutral response without invoking the HTTP requester.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenProspectExperienceIsDisabled_ShouldNotInvokeInnerRequester()
    {
        var context = BuildContext(ProspectRouteKey);
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                false,
                null,
                context.RequestAborted))
            .ReturnsAsync(false);
        var requester = CreateRequester();

        var result = await requester.GetResponse(context);

        result.IsError.Should().BeFalse();
        result.Data.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await result.Data.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        payload.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, int>("prospectEntitiesCount", 0));
        _innerRequester.Verify(
            inner => inner.GetResponse(It.IsAny<HttpContext>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that an enabled Prospect experience invokes the standard Ocelot requester unchanged.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenProspectExperienceIsEnabled_ShouldInvokeInnerRequester()
    {
        var context = BuildContext(ProspectRouteKey);
        var downstreamResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        var expected = new OkResponse<HttpResponseMessage>(downstreamResponse);
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                false,
                null,
                context.RequestAborted))
            .ReturnsAsync(true);
        _innerRequester
            .Setup(inner => inner.GetResponse(context))
            .ReturnsAsync(expected);
        var requester = CreateRequester();

        var result = await requester.GetResponse(context);

        result.Should().BeSameAs(expected);
        _innerRequester.Verify(inner => inner.GetResponse(context), Times.Once);
    }

    /// <summary>
    /// Verifies that non-Prospect routes bypass feature evaluation and use the standard requester.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenRouteIsNotWalletInfoProspect_ShouldInvokeInnerRequester()
    {
        var context = BuildContext(AccountRouteKey);
        var expected = new OkResponse<HttpResponseMessage>(new HttpResponseMessage(HttpStatusCode.OK));
        _innerRequester
            .Setup(inner => inner.GetResponse(context))
            .ReturnsAsync(expected);
        var requester = CreateRequester();

        var result = await requester.GetResponse(context);

        result.Should().BeSameAs(expected);
        _featureFlagService.Verify(
            service => service.IsEnabledAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that feature targeting uses the email carried by the authenticated bearer token.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenBearerTokenContainsEmail_ShouldUseEmailForFeatureTargeting()
    {
        var context = BuildContext(ProspectRouteKey, BuildJwt("user@test.fr"));
        FeatureContext? capturedContext = null;
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                false,
                It.IsAny<FeatureContext?>(),
                context.RequestAborted))
            .Callback<string, bool, FeatureContext?, CancellationToken>((_, _, featureContext, _) => capturedContext = featureContext)
            .ReturnsAsync(false);
        var requester = CreateRequester();

        await requester.GetResponse(context);

        capturedContext.Should().NotBeNull();
        capturedContext!.Email.Should().Be("user@test.fr");
        _innerRequester.Verify(inner => inner.GetResponse(It.IsAny<HttpContext>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a malformed token falls back to untargeted feature evaluation without invoking Prospect.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenBearerTokenIsMalformed_ShouldEvaluateWithoutTargeting()
    {
        var context = BuildContext(ProspectRouteKey, "not-a-jwt");
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                false,
                null,
                context.RequestAborted))
            .ReturnsAsync(false);
        var requester = CreateRequester();

        var result = await requester.GetResponse(context);

        result.Data.StatusCode.Should().Be(HttpStatusCode.OK);
        _innerRequester.Verify(inner => inner.GetResponse(It.IsAny<HttpContext>()), Times.Never);
    }

    /// <summary>
    /// Verifies that an unexpected feature service failure does not accidentally invoke the Prospect requester.
    /// </summary>
    [Fact]
    public async Task GetResponse_WhenFeatureFlagServiceThrows_ShouldNotInvokeInnerRequester()
    {
        var context = BuildContext(ProspectRouteKey);
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                false,
                null,
                context.RequestAborted))
            .ThrowsAsync(new InvalidOperationException("Feature provider failure"));
        var requester = CreateRequester();

        var act = () => requester.GetResponse(context);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _innerRequester.Verify(inner => inner.GetResponse(It.IsAny<HttpContext>()), Times.Never);
    }

    #endregion

    /// <summary>
    /// Creates the requester under test.
    /// </summary>
    /// <returns>The requester.</returns>
    private WalletInfoProspectFeatureFlagRequester CreateRequester()
    {
        return new WalletInfoProspectFeatureFlagRequester(
            _innerRequester.Object,
            _featureFlagService.Object,
            NullLogger<WalletInfoProspectFeatureFlagRequester>.Instance);
    }

    /// <summary>
    /// Builds an Ocelot context carrying the requested downstream route key.
    /// </summary>
    /// <param name="routeKey">The downstream route key.</param>
    /// <param name="bearerToken">The optional bearer token.</param>
    /// <returns>The downstream context.</returns>
    private static DefaultHttpContext BuildContext(string routeKey, string? bearerToken = null)
    {
        var context = new DefaultHttpContext();
        context.Items.UpsertDownstreamRoute(BuildRoute(routeKey));

        if (!string.IsNullOrEmpty(bearerToken))
        {
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        }

        return context;
    }

    /// <summary>
    /// Builds a minimal Ocelot downstream route.
    /// </summary>
    /// <param name="key">The route key.</param>
    /// <returns>The downstream route.</returns>
    private static DownstreamRoute BuildRoute(string key)
    {
        return new DownstreamRoute(
            key: key,
            upstreamPathTemplate: new UpstreamPathTemplate("template", 1, true, string.Empty),
            upstreamHeadersFindAndReplace: null,
            downstreamHeadersFindAndReplace: null,
            downstreamAddresses: null,
            serviceName: "serviceName",
            serviceNamespace: "serviceNamespace",
            httpHandlerOptions: null,
            qosOptions: null,
            downstreamScheme: "http",
            requestIdKey: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            rateLimitOptions: null,
            routeClaimsRequirement: null,
            claimsToQueries: null,
            claimsToHeaders: null,
            claimsToClaims: null,
            claimsToPath: null,
            authenticationOptions: null,
            downstreamPathTemplate: null,
            loadBalancerKey: null,
            delegatingHandlers: null,
            addHeadersToDownstream: null,
            addHeadersToUpstream: null,
            dangerousAcceptAnyServerCertificateValidator: false,
            securityOptions: null,
            downstreamHttpMethod: null,
            downstreamHttpVersion: null,
            downstreamHttpVersionPolicy: HttpVersionPolicy.RequestVersionOrLower,
            upstreamHeaders: null,
            metadataOptions: new MetadataOptions(new FileMetadataOptions()),
            timeout: null);
    }

    /// <summary>
    /// Builds a signed-format test JWT containing the supplied email claim.
    /// </summary>
    /// <param name="email">The email claim.</param>
    /// <returns>The serialized JWT.</returns>
    private static string BuildJwt(string email)
    {
        var token = new JwtSecurityToken(claims: [new Claim("upn", email)]);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
