using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Moq.Protected;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="MandatePaymentPreferencesClient"/>.
/// </summary>
public sealed class MandatePaymentPreferencesClientTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Verifies that GET returns the Mandat payment preference payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceExists_ReturnsPaymentPreference()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PaymentPreferenceResponse { PaymentType = "OTHER" }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var client = CreateClient(response, request => captured = request);

        var result = await client.GetAsync(42, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("OTHER");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences");
    }

    /// <summary>
    /// Verifies that GET maps Mandat not found responses to null.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceIsMissing_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetAsync(42, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GET throws when Mandat returns an empty successful payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((PaymentPreferenceResponse?)null, options: CamelCase)
        };
        var client = CreateClient(response);

        Func<Task> act = () => client.GetAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that POST OTHER sends the ContactEmail header.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenMandatAcceptsRequest_ReturnsTrue()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        var result = await client.SetOtherAsync(42, "user@test.fr", CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences/other");
        captured.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be("user@test.fr");
    }

    /// <summary>
    /// Verifies that POST OTHER maps Mandat not found responses to false.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenMandatReturnsNotFound_ReturnsFalse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.SetOtherAsync(42, "user@test.fr", CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that POST OTHER propagates unexpected Mandat failures.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenMandatFails_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadGateway));

        Func<Task> act = () => client.SetOtherAsync(42, "user@test.fr", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that DELETE sends the Mandat reset request.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenMandatAcceptsRequest_ReturnsTrue()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        var result = await client.ResetAsync(42, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Delete);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences");
    }

    /// <summary>
    /// Verifies that DELETE maps Mandat not found responses to false.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenMandatReturnsNotFound_ReturnsFalse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.ResetAsync(42, CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that DELETE propagates unexpected Mandat failures.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenMandatFails_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadGateway));

        Func<Task> act = () => client.ResetAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Creates the tested Mandat payment preferences client.
    /// </summary>
    /// <param name="response">The HTTP response to return.</param>
    /// <param name="capture">The optional request capture callback.</param>
    /// <returns>The tested client.</returns>
    private static MandatePaymentPreferencesClient CreateClient(
        HttpResponseMessage response,
        Action<HttpRequestMessage>? capture = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capture?.Invoke(request))
            .ReturnsAsync(response);

        return new MandatePaymentPreferencesClient(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://mandate.test/")
        });
    }
}
