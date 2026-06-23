using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Ocelot.Configuration;
using Ocelot.Configuration.File;
using Ocelot.Middleware;
using Ocelot.Values;
using System.Net;
using System.Net.Http.Json;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class FeatureFlagGateHandlerTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();
    private readonly IHttpContextAccessor _httpContextAccessor = new HttpContextAccessor();

    private (HttpMessageInvoker invoker, FeatureFlagGateHandler handler) CreateHandler(
        HttpResponseMessage? innerResponse = null)
    {
        var handler = new FeatureFlagGateHandler(
            _featureFlagServiceMock.Object,
            _httpContextAccessor,
            NullLogger<FeatureFlagGateHandler>.Instance);

        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(innerResponse ?? new HttpResponseMessage(HttpStatusCode.OK));
        handler.InnerHandler = innerMock.Object;

        return (new HttpMessageInvoker(handler), handler);
    }

    private void SetupHttpContext(string path, Dictionary<string, string>? metadata = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        if (metadata != null)
        {
            var fileMetadata = new FileMetadataOptions { Metadata = metadata };
            var downstreamRoute = new DownstreamRoute(
                key: "test-route",
                upstreamPathTemplate: new UpstreamPathTemplate(path, 1, true, ""),
                upstreamHeadersFindAndReplace: null,
                downstreamHeadersFindAndReplace: null,
                downstreamAddresses: null,
                serviceName: "serviceName",
                serviceNamespace: "serviceNamespace",
                httpHandlerOptions: null,
                useServiceDiscovery: false,
                enableEndpointEndpointRateLimiting: false,
                qosOptions: null,
                downstreamScheme: "http",
                requestIdKey: null,
                isCached: false,
                cacheOptions: null,
                loadBalancerOptions: null,
                rateLimitOptions: null,
                routeClaimsRequirement: null,
                claimsToQueries: null,
                claimsToHeaders: null,
                claimsToClaims: null,
                claimsToPath: null,
                isAuthenticated: false,
                isAuthorized: false,
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
                downstreamHttpVersionPolicy: default,
                upstreamHeaders: null,
                metadataOptions: new MetadataOptions(fileMetadata)
            );

            httpContext.Items.UpsertDownstreamRoute(downstreamRoute);
        }

        _httpContextAccessor.HttpContext = httpContext;
    }

    [Fact]
    public async Task Should_Return_403_When_Flag_Is_Disabled()
    {
        // Arrange
        SetupHttpContext("/gtw/prospect/api/search",
            new Dictionary<string, string> { ["featureFlag"] = "isProspectEnabled" });
        _featureFlagServiceMock
            .Setup(s => s.IsEnabledAsync("isProspectEnabled", false, It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/search");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await result.Content.ReadFromJsonAsync<ErrorResponse>();
        content!.ErrorCode.Should().Be(Errors.FeatureFlagDisabledCode);
    }

    [Fact]
    public async Task Should_Forward_Request_When_Flag_Is_Enabled()
    {
        // Arrange
        SetupHttpContext("/gtw/prospect/api/search",
            new Dictionary<string, string> { ["featureFlag"] = "isProspectEnabled" });
        _featureFlagServiceMock
            .Setup(s => s.IsEnabledAsync("isProspectEnabled", false, It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/search");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Forward_When_No_FeatureFlag_Metadata()
    {
        // Arrange - route with no featureFlag metadata
        SetupHttpContext("/health", new Dictionary<string, string>());
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/health");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _featureFlagServiceMock.Verify(
            s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Build_FeatureContext_From_ContactEmail_Header()
    {
        // Arrange
        SetupHttpContext("/gtw/booking/api/link",
            new Dictionary<string, string> { ["featureFlag"] = "isBookingEnabled" });
        FeatureContext? capturedContext = null;
        _featureFlagServiceMock
            .Setup(s => s.IsEnabledAsync("isBookingEnabled", false, It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, FeatureContext?, CancellationToken>((_, _, ctx, _) => capturedContext = ctx)
            .ReturnsAsync(true);

        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/link");
        request.Headers.Add("ContactEmail", "user@test.fr");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Email.Should().Be("user@test.fr");
    }

    [Fact]
    public async Task Should_Pass_Null_Context_When_No_ContactEmail_Header()
    {
        // Arrange
        SetupHttpContext("/gtw/booking/api/link",
            new Dictionary<string, string> { ["featureFlag"] = "isBookingEnabled" });
        _featureFlagServiceMock
            .Setup(s => s.IsEnabledAsync("isBookingEnabled", false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/link");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Use_Exact_Metadata_Value_As_FlagKey()
    {
        // Arrange
        const string customFlagKey = "my-custom/flag-key";
        SetupHttpContext("/gtw/booking/api/v2/link",
            new Dictionary<string, string> { ["featureFlag"] = customFlagKey });
        _featureFlagServiceMock
            .Setup(s => s.IsEnabledAsync(customFlagKey, false, It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/v2/link");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _featureFlagServiceMock.Verify(
            s => s.IsEnabledAsync(customFlagKey, false, It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Forward_When_DownstreamRoute_Is_Null()
    {
        // Arrange - no DownstreamRoute in HttpContext Items
        SetupHttpContext("/gtw/booking/api/link");
        var (invoker, _) = CreateHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/link");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _featureFlagServiceMock.Verify(
            s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private record ErrorResponse(string ErrorCode, string ErrorMessage);
}
