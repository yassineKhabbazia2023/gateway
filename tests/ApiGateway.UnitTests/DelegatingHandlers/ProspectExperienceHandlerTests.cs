using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
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
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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

    /// <summary>
    /// Preuve directe de l'exigence « aucun appel ne doit partir vers pulse.back.prospect » : flag coupé,
    /// le handler interne — qui porte l'appel HTTP réel vers le downstream — n'est jamais invoqué.
    /// S'applique à la route WalletInfoProspect agrégée dans /gtw/wallet/api/infos/currentuser.
    /// </summary>
    [Fact]
    public async Task Should_Not_Call_Downstream_When_ProspectExperience_Is_Disabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new ProspectExperienceHandler(featureFlagService.Object, NullLogger<ProspectExperienceHandler>.Instance)
        {
            InnerHandler = innerMock.Object
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://appcegpulseprs01.azurewebsites.net/api/entity-count/currentuser");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        innerMock.Protected().Verify<Task<HttpResponseMessage>>("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Should_Forward_Request_When_ProspectExperience_Is_Enabled()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c != null && c.Email == expectedEmail), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(featureFlagService, innerResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(expectedEmail));

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c != null && c.Email == expectedEmail), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Pass_Null_Email_When_No_Authorization_Header()
    {
        // Arrange
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var (invoker, _) = CreateHandler(featureFlagService);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/prospect/api/some-endpoint");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        featureFlagService.Verify(
            s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that enabled Prospect routes forward multipart content unchanged and return downstream success.
    /// </summary>
    [Fact]
    public async Task Should_Forward_Multipart_Request_And_Downstream_Success_Response()
    {
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        HttpRequestMessage? capturedRequest = null;
        var downstreamResponse = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new
            {
                beneficiaryId = Guid.NewGuid(),
                documentType = "PASSPORT",
                documents = Array.Empty<object>()
            })
        };
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(downstreamResponse);
        var handler = new ProspectExperienceHandler(
            featureFlagService.Object,
            NullLogger<ProspectExperienceHandler>.Instance)
        {
            InnerHandler = innerMock.Object
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var multipart = new MultipartFormDataContent("identity-boundary");
        multipart.Add(new StringContent("PASSPORT"), "documentType");
        var fileContent = new ByteArrayContent("%PDF-"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        multipart.Add(fileContent, "files", "passport.pdf");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.test.com/api/prospects/123/beneficiaries/148cf2c3-ccfa-44d0-addd-1b46a4a6f2f9/identity-documents")
        {
            Content = multipart
        };

        var result = await invoker.SendAsync(request, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().BeSameAs(downstreamResponse);
        capturedRequest.Should().BeSameAs(request);
        capturedRequest!.Content.Should().BeSameAs(multipart);
        capturedRequest.Content.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
        capturedRequest.Content.Headers.ContentType.Parameters
            .Single(parameter => parameter.Name == "boundary")
            .Value.Trim('"')
            .Should()
            .Be("identity-boundary");
    }

    /// <summary>
    /// Verifies that enabled Prospect routes propagate downstream problem responses unchanged.
    /// </summary>
    [Fact]
    public async Task Should_Propagate_Downstream_Problem_Response()
    {
        const string problemJson =
            "{\"title\":\"Only PDF, JPEG, and PNG files are supported.\",\"status\":415}";
        var featureFlagService = new Mock<IFeatureFlagService>();
        featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsProspectExperienceEnabled,
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var downstreamResponse = new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType)
        {
            Content = new StringContent(
                problemJson,
                System.Text.Encoding.UTF8,
                "application/problem+json")
        };
        var (invoker, _) = CreateHandler(featureFlagService, downstreamResponse);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.test.com/api/prospects/123/beneficiaries/148cf2c3-ccfa-44d0-addd-1b46a4a6f2f9/identity-documents");

        var result = await invoker.SendAsync(request, CancellationToken.None);

        result.Should().BeSameAs(downstreamResponse);
        result.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        result.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await result.Content.ReadAsStringAsync()).Should().Be(problemJson);
    }
}
