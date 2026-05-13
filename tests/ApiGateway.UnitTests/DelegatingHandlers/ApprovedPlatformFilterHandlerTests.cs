using ApiGateway.DelegatingHandlers;
using ApiGateway.FeatureFlags;
using ApiGateway.Offer.Constants;
using Moq.Protected;
using System.Net;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class ApprovedPlatformFilterHandlerTests
{
    private static string GenerateDummyJwtToken(string email)
    {
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(new { email })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.";
    }

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

        var handler = new ApprovedPlatformFilterHandler(featureFlagService.Object)
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
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
    public async Task Should_Pass_UserEmail_To_FeatureFlagService()
    {
        // Arrange
        const string expectedEmail = "user@test.fr";
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, expectedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var (invoker, _) = CreateInvoker(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/offer/api/offers/8");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(expectedEmail));

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, expectedEmail, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
