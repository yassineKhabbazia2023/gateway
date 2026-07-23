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
            Content = JsonContent.Create(
                new MandatePaymentPreferenceResponse
                {
                    PaymentType = "MANDATE_SEPA",
                    Iban = "FR7630006000011234567890189",
                    Bic = "AGRIFRPP"
                },
                options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var client = CreateClient(response, request => captured = request);

        var result = await client.GetAsync(42, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        result.Iban.Should().Be("FR7630006000011234567890189");
        result.Bic.Should().Be("AGRIFRPP");
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
            Content = JsonContent.Create((MandatePaymentPreferenceResponse?)null, options: CamelCase)
        };
        var client = CreateClient(response);

        Func<Task> act = () => client.GetAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that bank-details extraction posts the request body and returns the typed response.
    /// </summary>
    [Fact]
    public async Task ExtractBankDetailsAsync_WhenMandatAcceptsIdentifiers_ReturnsExtractedDetails()
    {
        HttpRequestMessage? captured = null;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MandateBankDetailsExtractionResponse
            {
                Iban = new MandateExtractedIbanResponse
                {
                    CountryCode = "FR",
                    CheckDigits = "76",
                    BankAccountPart = "30006000011234567890189"
                },
                Rib = new MandateExtractedRibResponse
                {
                    BankCode = "30006",
                    BranchCode = "00001",
                    AccountNumber = "12345678901",
                    RibKey = "89"
                },
                Bic = new MandateExtractedBicResponse
                {
                    CountryCode = "FR",
                    BankCode = "AGRI",
                    LocationCode = "FR",
                    BranchCode = "PP"
                },
                Domiciliation = "AGRI"
            }, options: CamelCase)
        };
        var client = CreateClient(response, request => captured = request);

        var result = await client.ExtractBankDetailsAsync(
            "FR7630006000011234567890189",
            "AGRIFRPP",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Rib.BankCode.Should().Be("30006");
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/bank-details/extract");
        using var payload = JsonDocument.Parse(await captured.Content!.ReadAsStringAsync());
        payload.RootElement.GetProperty("iban").GetString().Should().Be("FR7630006000011234567890189");
        payload.RootElement.GetProperty("bic").GetString().Should().Be("AGRIFRPP");
    }

    /// <summary>
    /// Verifies that Mandat validation failures are returned as an unavailable extraction result.
    /// </summary>
    [Fact]
    public async Task ExtractBankDetailsAsync_WhenMandatRejectsIdentifiers_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await client.ExtractBankDetailsAsync("invalid", "invalid", CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an empty successful extraction response is treated as a downstream failure.
    /// </summary>
    [Fact]
    public async Task ExtractBankDetailsAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((MandateBankDetailsExtractionResponse?)null, options: CamelCase)
        };
        var client = CreateClient(response);

        Func<Task> act = () => client.ExtractBankDetailsAsync("iban", "bic", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that unexpected Mandat extraction failures propagate to the Gateway error pipeline.
    /// </summary>
    [Fact]
    public async Task ExtractBankDetailsAsync_WhenMandatFails_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadGateway));

        Func<Task> act = () => client.ExtractBankDetailsAsync("iban", "bic", CancellationToken.None);

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
    /// Verifies that mark-sent-to-akuiteo sends the expected Mandat request.
    /// </summary>
    [Fact]
    public async Task MarkSentToAkuiteoAsync_WhenMandatAcceptsRequest_ReturnsTrue()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        var result = await client.MarkSentToAkuiteoAsync(42, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences/mark-sent-to-akuiteo");
    }

    /// <summary>
    /// Verifies that mark-sent-to-akuiteo maps not found responses to false.
    /// </summary>
    [Fact]
    public async Task MarkSentToAkuiteoAsync_WhenMandatReturnsNotFound_ReturnsFalse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.MarkSentToAkuiteoAsync(42, CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that saving the signed mandate document identifier sends the expected Mandat request.
    /// </summary>
    [Fact]
    public async Task SaveSignedMandateDocumentIdAsync_WhenMandatAcceptsRequest_ReturnsTrue()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        var result = await client.SaveSignedMandateDocumentIdAsync(42, CancellationToken.None, "DOC/123");

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences/signed-mandate-document-id?signedMandateDocumentId=DOC%2F123");
    }

    /// <summary>
    /// Verifies that saving the signed mandate document identifier maps not found responses to false.
    /// </summary>
    [Fact]
    public async Task SaveSignedMandateDocumentIdAsync_WhenMandatReturnsNotFound_ReturnsFalse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.SaveSignedMandateDocumentIdAsync(42, CancellationToken.None, "DOC-123");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the signed mandate document identifier is read from Mandat.
    /// </summary>
    [Fact]
    public async Task GetSignedMandateDocumentIdAsync_WhenMandatReturnsDocumentId_ReturnsDocumentId()
    {
        HttpRequestMessage? captured = null;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create("456", options: CamelCase)
        };
        var client = CreateClient(response, request => captured = request);

        var result = await client.GetSignedMandateDocumentIdAsync(42, CancellationToken.None);

        result.Should().Be("456");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/payment-preferences/sepa/signed-mandate-document-id");
    }

    /// <summary>
    /// Verifies that the signed mandate document identifier is read when Mandat returns plain text.
    /// </summary>
    [Fact]
    public async Task GetSignedMandateDocumentIdAsync_WhenMandatReturnsPlainTextDocumentId_ReturnsDocumentId()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("456")
        };
        var client = CreateClient(response);

        var result = await client.GetSignedMandateDocumentIdAsync(42, CancellationToken.None);

        result.Should().Be("456");
    }

    /// <summary>
    /// Verifies that a missing signed mandate document identifier is mapped to null.
    /// </summary>
    [Fact]
    public async Task GetSignedMandateDocumentIdAsync_WhenMandatReturnsNotFound_ReturnsNull()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetSignedMandateDocumentIdAsync(42, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that cleanup sends the expected Mandat request.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenMandatAcceptsRequest_ReturnsTrue()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        var result = await client.CleanupAsync(42, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://mandate.test/api/onboarding/42/cleanup");
    }

    /// <summary>
    /// Verifies that cleanup maps Mandat not found responses to false.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenMandatReturnsNotFound_ReturnsFalse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.CleanupAsync(42, CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that cleanup propagates unexpected Mandat failures.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenMandatFails_Throws()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadGateway));

        Func<Task> act = () => client.CleanupAsync(42, CancellationToken.None);

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
