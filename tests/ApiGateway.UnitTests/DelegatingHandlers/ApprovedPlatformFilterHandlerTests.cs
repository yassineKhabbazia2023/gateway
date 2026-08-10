using ApiGateway.DelegatingHandlers;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Offer.Constants;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using System.Net;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class ApprovedPlatformFilterHandlerTests
{
    private static (HttpMessageInvoker Invoker, Func<HttpRequestMessage?> GetCapturedRequest) CreateInvoker(
        Mock<IFeatureFlagService> featureFlagService)
    {
        HttpRequestMessage? captured = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new ApprovedPlatformFilterHandler(featureFlagService.Object, Mock.Of<ILogger<ApprovedPlatformFilterHandler>>())
        {
            InnerHandler = innerMock.Object
        };
        return (new HttpMessageInvoker(handler), () => captured);
    }

    [Fact]
    public async Task Should_Add_ExcludePlanCodes_Param_When_Flag_Is_Disabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, getCaptured) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var captured = getCaptured();
        captured.Should().NotBeNull();
        captured!.RequestUri!.Query.Should().Contain($"excludePlanCodes={OfferPlanCodes.ApprovedPlatform}");
    }

    [Fact]
    public async Task Should_Not_Modify_Request_When_Flag_Is_Enabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var (invoker, getCaptured) = CreateInvoker(featureFlagService);
        const string originalUri = "https://api.test.com/offer/api/offers/8";
        var request = new HttpRequestMessage(HttpMethod.Get, originalUri);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var captured = getCaptured();
        captured!.RequestUri!.ToString().Should().Be(originalUri);
    }

    [Fact]
    public async Task Should_Append_ExcludePlanCodes_To_Existing_Query_String_When_Flag_Is_Disabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, getCaptured) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8?foo=bar");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var captured = getCaptured();
        captured!.RequestUri!.Query.Should().Contain("foo=bar");
        captured.RequestUri.Query.Should().Contain($"excludePlanCodes={OfferPlanCodes.ApprovedPlatform}");
    }

    [Fact]
    public async Task Should_Pass_ContactId_From_CurrentUser_Header_To_FeatureFlagService()
    {
        // Arrange
        const string expectedContactId = "12345";
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c != null && c.ContactId == expectedContactId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var (invoker, _) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8");
        request.Headers.Add("CurrentUser", expectedContactId);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c != null && c.ContactId == expectedContactId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Exclude_Plan_When_Flag_Evaluation_Throws()
    {
        // Arrange : le provider de feature flags est en erreur, le handler doit dégrader en fail closed
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("ConfigCat KO"));
        var (invoker, getCaptured) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8");

        // Act : aucune exception ne doit remonter vers le client
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert : traité comme flag désactivé → plan masqué
        var captured = getCaptured();
        captured.Should().NotBeNull();
        captured!.RequestUri!.Query.Should().Contain($"excludePlanCodes={OfferPlanCodes.ApprovedPlatform}");
    }

    [Fact]
    public async Task Should_Evaluate_Without_Context_When_CurrentUser_Header_Is_Missing()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, _) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert : sans header CurrentUser, le contexte est null (fail closed côté règle de ciblage)
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
