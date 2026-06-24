using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Internal;
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
    /// Verifies that POST SEPA sends the expected Mandat route and payload.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenMandatReturnsSignatureUrl_ReturnsSignatureUrl()
    {
        HttpRequestMessage? captured = null;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SepaPaymentPreferenceResponse { SignatureUrl = "https://signature.test" }, options: CamelCase)
        };
        var client = CreateClient(response, request => captured = request);
        var request = new MandateSepaPaymentPreferenceRequest
        {
            DocumentId = 123,
            AccountHolder = "Jean Dupont",
            Address = "10 rue de Paris",
            AddressLine2 = "Batiment A",
            City = "Paris",
            Country = "France",
            PostalCode = "75008",
            Iban = "FR7630006000011234567890189",
            Bic = "AGRIFRPP",
            RecipientEmail = "jean.dupont@test.fr",
            RecipientFirstName = "Jean",
            RecipientLastName = "Dupont"
        };

        var result = await client.SetSepaAsync(42, request, CancellationToken.None);

        result.Should().Be("https://signature.test");
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences/sepa");
        var payload = JsonDocument.Parse(await captured.Content!.ReadAsStringAsync());
        payload.RootElement.GetProperty("documentId").GetInt32().Should().Be(123);
        payload.RootElement.TryGetProperty("ribDocumentContent", out _).Should().BeFalse();
        payload.RootElement.TryGetProperty("ribFileName", out _).Should().BeFalse();
        payload.RootElement.GetProperty("accountHolder").GetString().Should().Be("Jean Dupont");
        payload.RootElement.GetProperty("address").GetString().Should().Be("10 rue de Paris");
        payload.RootElement.GetProperty("addressLine2").GetString().Should().Be("Batiment A");
        payload.RootElement.GetProperty("city").GetString().Should().Be("Paris");
        payload.RootElement.GetProperty("country").GetString().Should().Be("France");
        payload.RootElement.GetProperty("postalCode").GetString().Should().Be("75008");
        payload.RootElement.GetProperty("recipientEmail").GetString().Should().Be("jean.dupont@test.fr");
    }

    /// <summary>
    /// Verifies that POST SEPA maps Mandat not found responses to null.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenMandatReturnsNotFound_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.SetSepaAsync(42, new MandateSepaPaymentPreferenceRequest(), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that POST SEPA propagates unexpected Mandat failures.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenMandatFails_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadGateway));

        Func<Task> act = () => client.SetSepaAsync(42, new MandateSepaPaymentPreferenceRequest(), CancellationToken.None);

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
