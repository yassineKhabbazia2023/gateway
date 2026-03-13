using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class ProspectExperienceHandlerTests
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

    private static (HttpMessageInvoker invoker, ProspectExperienceHandler handler) CreateHandler(
        Mock<IFeatureFlagService> featureFlagService, HttpResponseMessage? innerResponse = null)
    {
        var handler = new ProspectExperienceHandler(featureFlagService.Object, NullLogger<ProspectExperienceHandler>.Instance);

        if (innerResponse is not null)
        {
            var innerMock = new Mock<HttpMessageHandler>();
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(innerResponse);
            handler.InnerHandler = innerMock.Object;
        }

        return (new HttpMessageInvoker(handler), handler);
    }

    [Fact]
    public async Task Should_Return_403_When_ProspectExperience_Is_Disabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, _) = CreateHandler(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Forbidden,
            Content = JsonContent.Create(new { ErrorCode = Errors.ProspectExperienceDisabledCode, ErrorMessage = Errors.ProspectExperienceDisabledMessage })
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Forward_Request_When_ProspectExperience_Is_Enabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(featureFlagService, innerResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Pass_UserEmail_To_FeatureFlagService()
    {
        // Arrange
        const string expectedEmail = "user@test.fr";
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, expectedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(featureFlagService, innerResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(expectedEmail));

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, expectedEmail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Pass_Null_Email_When_No_Authorization_Header()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, _) = CreateHandler(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
