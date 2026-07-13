using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;

namespace ApiGateway.UnitTests.DelegatingHandlers;

/// <summary>
/// Unit tests for <see cref="QaSensitiveEndpointsHandler"/>.
/// </summary>
public sealed class QaSensitiveEndpointsHandlerTests
{
    /// <summary>
    /// Verifies that the handler blocks requests when QA-sensitive endpoints are disabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenQaSensitiveEndpointsAreDisabled_ReturnsForbidden()
    {
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        using var invoker = CreateInvoker(featureFlagService, innerResponse: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/gtw/prospect/api/onboarding/42/cleanup");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        var result = await invoker.SendAsync(request, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errorCode").GetString().Should().Be(Errors.FeatureFlagDisabledCode);
    }

    /// <summary>
    /// Verifies that the handler forwards requests when QA-sensitive endpoints are enabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenQaSensitiveEndpointsAreEnabled_ForwardsRequest()
    {
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var downstreamResponse = new HttpResponseMessage(HttpStatusCode.NoContent);
        using var invoker = CreateInvoker(featureFlagService, downstreamResponse);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/gtw/prospect/api/onboarding/42/cleanup");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        var result = await invoker.SendAsync(request, CancellationToken.None);

        result.Should().BeSameAs(downstreamResponse);
    }

    /// <summary>
    /// Verifies that the handler passes the connected user email to ConfigCat.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenBearerTokenContainsEmail_PassesEmailFeatureContext()
    {
        const string expectedEmail = "user@test.fr";
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.Is<FeatureContext?>(context => context != null && context.Email == expectedEmail),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        using var invoker = CreateInvoker(featureFlagService, new HttpResponseMessage(HttpStatusCode.OK));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/gtw/prospect/api/onboarding/42/cleanup");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(expectedEmail));

        await invoker.SendAsync(request, CancellationToken.None);

        featureFlagService.Verify(
            service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.Is<FeatureContext?>(context => context != null && context.Email == expectedEmail),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that the handler uses no feature context when no bearer token is present.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenAuthorizationHeaderIsMissing_PassesNullFeatureContext()
    {
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.Is<FeatureContext?>(context => context == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        using var invoker = CreateInvoker(featureFlagService, innerResponse: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/gtw/prospect/api/onboarding/42/cleanup");

        await invoker.SendAsync(request, CancellationToken.None);

        featureFlagService.Verify(
            service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.Is<FeatureContext?>(context => context == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Creates an HTTP invoker using the tested handler.
    /// </summary>
    /// <param name="featureFlagService">The feature flag service mock.</param>
    /// <param name="innerResponse">The optional inner handler response.</param>
    /// <returns>The HTTP message invoker.</returns>
    private static HttpMessageInvoker CreateInvoker(
        Mock<IFeatureFlagService> featureFlagService,
        HttpResponseMessage? innerResponse)
    {
        var handler = new QaSensitiveEndpointsHandler(
            featureFlagService.Object,
            NullLogger<QaSensitiveEndpointsHandler>.Instance);

        if (innerResponse is not null)
        {
            var innerMock = new Mock<HttpMessageHandler>();
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(innerResponse);
            handler.InnerHandler = innerMock.Object;
        }

        return new HttpMessageInvoker(handler);
    }

    /// <summary>
    /// Generates an unsigned dummy JWT token containing an email claim.
    /// </summary>
    /// <param name="email">The email claim value.</param>
    /// <returns>The dummy JWT token.</returns>
    private static string GenerateDummyJwtToken(string email)
    {
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(new { email })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.";
    }
}
