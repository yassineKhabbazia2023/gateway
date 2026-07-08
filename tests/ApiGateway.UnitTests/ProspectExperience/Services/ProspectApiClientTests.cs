using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Constants;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq.Protected;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

public class ProspectApiClientTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static (ProspectApiClient client, Mock<HttpMessageHandler> handler) CreateClient(HttpResponseMessage response, Action<HttpRequestMessage>? capture = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capture?.Invoke(req))
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        return (new ProspectApiClient(httpClient, logger.Object), handler);
    }

    private static CreateProspectRequest BuildRequest(string siret = "12345678901234") => new()
    {
        Siret = siret,
        LegalForm = "SARL",
        LegalStructure = "PERSONNE_MORALE",
        CaseManagerContactId = 100,
        AccountManagerContactId = 200,
        OfficeCode = "PAR",
        Department = "75",
        Region = "11",
        Country = "FR",
        DossierCAC = false,
        Signatory = new SignatoryDto
        {
            Title = "M", LastName = "Dupont", FirstName = "Jean", JobTitle = "Dir",
            Department = "Direction", CompanyRole = "PRESIDENT_GERANT",
            ContactTypes = ["SIGNATAIRE"], Email = "jean@test.fr", MobilePhone = "0612345678"
        }
    };

    private static InpiCompanyInfo BuildInpi(string siret = "12345678901234") => new()
    {
        Siret = siret,
        Siren = siret[..9],
        LegalName = "ACME SARL",
        Address = "1 rue de la Paix",
        ZipCode = "75002",
        City = "Paris",
        LegalForm = "SARL",
        NafCode = "6201Z",
        ShareCapital = 10000,
        RegionCode = "11"
    };

    /// <summary>
    /// Verifies that payment preference notifications are sent to the expected Prospect route with the expected payload.
    /// </summary>
    [Fact]
    public async Task SendPaymentPreferenceNotificationsAsync_WhenCalled_PostsExpectedPayload()
    {
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent), request => captured = request);

        await client.SendPaymentPreferenceNotificationsAsync(
            10,
            "signatory@test.fr",
            ["bs1@test.fr", "bs2@test.fr"],
            CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/10/payment-preferences/notifications");
        var payload = JsonDocument.Parse(await captured.Content!.ReadAsStringAsync());
        payload.RootElement.GetProperty("signatoryEmail").GetString().Should().Be("signatory@test.fr");
        payload.RootElement.GetProperty("collabReceiversEmails").EnumerateArray()
            .Select(element => element.GetString())
            .Should().Equal("bs1@test.fr", "bs2@test.fr");
    }

    [Fact]
    public async Task GetInpiCompanyInfoAsync_IssuesGetAndMapsExternalCompanyResponse()
    {
        var expected = new ExternalCompanyResponse
        {
            DisplayName = "ACME SARL",
            Siret = "12345678901234",
            Siren = "123456789",
            ApeCode = "6201Z",
            Address = new ExternalCompanyAddressResponse
            {
                Line1 = "1 rue de la Paix",
                PostalCode = "75002",
                City = "Paris",
                DepartmentCode = "75",
                RegionCode = "11",
                CountryCode = "FR"
            }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected, options: CamelCase)
        };

        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetInpiCompanyInfoAsync("12345678901234", CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.PathAndQuery.Should().Be("/api/accounts/external-companies/12345678901234");
        result.Siret.Should().Be(expected.Siret);
        result.Siren.Should().Be(expected.Siren);
        result.LegalName.Should().Be(expected.DisplayName);
        result.NafCode.Should().Be(expected.ApeCode);
        result.Address.Should().Be(expected.Address.Line1);
        result.ZipCode.Should().Be(expected.Address.PostalCode);
        result.City.Should().Be(expected.Address.City);
        result.RegionCode.Should().Be(expected.Address.RegionCode);
    }

    [Fact]
    public async Task GetInpiCompanyInfoAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((ExternalCompanyResponse?)null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetInpiCompanyInfoAsync("12345678901234", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetInpiCompanyInfoAsync_WhenResponseIsNotSuccessful_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetInpiCompanyInfoAsync("x", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetIncompleteProspectBySiretAsync_WhenProspectExists_ReturnsPayload()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                prospectId = 42,
                legalName = "ACME SARL",
                creationStatus = 2,
                lastCompletedStep = 6,
                completedMilestone = "RydgeAccountCreated",
                akuiteoAccountNumber = "AK-123",
                pendingAccountId = 99,
                pendingSignatoryContactId = 77,
                resumeRequestFingerprint = "fingerprint"
            }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetIncompleteProspectBySiretAsync("12345678901234", CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/incomplete-by-siret/12345678901234");
        result.Should().NotBeNull();
        result!.ProspectId.Should().Be(42);
        result.LegalName.Should().Be("ACME SARL");
        result.LastCompletedStep.Should().Be(6);
        result.CompletedMilestone.Should().Be(ProspectCreationMilestone.RydgeAccountCreated);
        result.AkuiteoAccountNumber.Should().Be("AK-123");
        result.PendingAccountId.Should().Be(99);
        result.PendingSignatoryContactId.Should().Be(77);
        result.ResumeRequestFingerprint.Should().Be("fingerprint");
    }

    [Fact]
    public async Task GetIncompleteProspectBySiretAsync_WhenProspectDoesNotExist_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetIncompleteProspectBySiretAsync("12345678901234", CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the document upload plan endpoint is called with the escaped step name and deserialized.
    /// </summary>
    [Fact]
    public async Task GetDocumentsToUploadToExternalServiceAsync_WhenPlanExists_ReturnsPlan()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                akuiteoAccountNumber = "AK-001",
                documentIds = new[] { 1, 2 },
                status = "PendingDocuments"
            }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary Step", CancellationToken.None);

        result.Should().NotBeNull();
        result!.AkuiteoAccountNumber.Should().Be("AK-001");
        result.DocumentIds.Should().Equal(1, 2);
        result.Status.Should().Be(DocumentsToUploadToExternalServiceStatus.PendingDocuments);
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.PathAndQuery.Should().Be("/api/prospects/42/documents/to-upload?stepName=Beneficiary%20Step");
    }

    /// <summary>
    /// Verifies that a missing upload plan returns null.
    /// </summary>
    [Fact]
    public async Task GetDocumentsToUploadToExternalServiceAsync_WhenPlanIsMissing_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an empty upload plan response throws a transport exception.
    /// </summary>
    [Fact]
    public async Task GetDocumentsToUploadToExternalServiceAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((DocumentsToUploadToExternalServiceResponse?)null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that document content and download metadata are mapped from Prospect.
    /// </summary>
    [Fact]
    public async Task GetDocumentAsync_WhenDocumentExists_ReturnsContentAndMetadata()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        };
        response.Content.Headers.ContentType = new("application/pdf");
        response.Content.Headers.ContentDisposition = new("attachment")
        {
            FileNameStar = "PASSEPORT_DUPONT_Jean"
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.GetDocumentAsync(42, 7, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Content.Should().Equal(1, 2, 3);
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("PASSEPORT_DUPONT_Jean");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/documents/7");
    }

    /// <summary>
    /// Verifies that missing document metadata uses empty-string fallbacks.
    /// </summary>
    [Fact]
    public async Task GetDocumentAsync_WhenMetadataIsMissing_ReturnsEmptyMetadata()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1])
        };
        var (client, _) = CreateClient(response);

        var result = await client.GetDocumentAsync(42, 7, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ContentType.Should().BeEmpty();
        result.FileName.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that quoted content disposition file names are unquoted.
    /// </summary>
    [Fact]
    public async Task GetDocumentAsync_WhenFileNameIsQuoted_ReturnsUnquotedFileName()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1])
        };
        response.Content.Headers.ContentDisposition = new("attachment")
        {
            FileName = "\"document.pdf\""
        };
        var (client, _) = CreateClient(response);

        var result = await client.GetDocumentAsync(42, 7, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FileName.Should().Be("document.pdf");
    }

    /// <summary>
    /// Verifies that a missing document returns null.
    /// </summary>
    [Fact]
    public async Task GetDocumentAsync_WhenDocumentIsMissing_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetDocumentAsync(42, 7, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that document upload sends multipart content, document type, and current user metadata.
    /// </summary>
    [Fact]
    public async Task UploadDocumentAsync_WhenProspectAcceptsUpload_ReturnsDocumentId()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { documentId = 123 }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        string? multipartBody = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            multipartBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await client.UploadDocumentAsync(42, 7, "user@test.fr", "RIB", file, CancellationToken.None);

        result.Should().Be(123);
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/documents");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("7");
        captured.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be("user@test.fr");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
        multipartBody.Should().Contain("name=documentType");
        multipartBody.Should().Contain("RIB");
        multipartBody.Should().Contain("filename=rib.pdf");
    }

    /// <summary>
    /// Verifies that document upload maps missing prospects to null.
    /// </summary>
    [Fact]
    public async Task UploadDocumentAsync_WhenProspectIsMissing_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);
        var file = new FormFile(new MemoryStream([1]), 0, 1, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await client.UploadDocumentAsync(42, 7, null, "RIB", file, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that document upload does not send a contact email header when the email is blank.
    /// </summary>
    [Fact]
    public async Task UploadDocumentAsync_WhenContactEmailIsBlank_DoesNotSendContactEmailHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { documentId = 123 }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);
        var file = new FormFile(new MemoryStream([1]), 0, 1, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await client.UploadDocumentAsync(42, 7, " ", "RIB", file, CancellationToken.None);

        result.Should().Be(123);
        captured!.Headers.Contains("ContactEmail").Should().BeFalse();
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("7");
    }

    /// <summary>
    /// Verifies that document upload reports an invalid downstream response when the payload is empty.
    /// </summary>
    [Fact]
    public async Task UploadDocumentAsync_WhenResponseIsEmpty_ThrowsHttpRequestException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create<object?>(null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);
        var file = new FormFile(new MemoryStream([1]), 0, 1, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        Func<Task> act = () => client.UploadDocumentAsync(42, 7, "user@test.fr", "RIB", file, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Document upload response for prospect 42 was empty.");
    }

    /// <summary>
    /// Verifies that document upload results are posted and deserialized.
    /// </summary>
    [Fact]
    public async Task RegisterDocumentUploadResultAsync_WhenPersisted_ReturnsResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                succeededDocumentIds = new[] { 1 },
                failedDocumentIds = new[] { 2 }
            }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            bodyJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.RegisterDocumentUploadResultAsync(
            42,
            new DocumentUploadResultRequest("Beneficiary", [1], [2]),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.SucceededDocumentIds.Should().Equal(1);
        result.FailedDocumentIds.Should().Equal(2);
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/documents/upload-result");
        bodyJson.Should().Contain("\"stepName\":\"Beneficiary\"");
    }

    /// <summary>
    /// Verifies that a missing document upload-result target returns null.
    /// </summary>
    [Fact]
    public async Task RegisterDocumentUploadResultAsync_WhenTargetIsMissing_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.RegisterDocumentUploadResultAsync(
            42,
            new DocumentUploadResultRequest("Beneficiary", [], []),
            CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that empty document upload-result responses throw a transport exception.
    /// </summary>
    [Fact]
    public async Task RegisterDocumentUploadResultAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((DocumentUploadResultResponse?)null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.RegisterDocumentUploadResultAsync(
            42,
            new DocumentUploadResultRequest("Beneficiary", [], []),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that complete-step calls the generic Prospect endpoint with an escaped step name and account identifier.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenProspectAcceptsCompletion_IssuesPutWithAccountId()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        await client.CompleteStepAsync(42, "Commercial Proposal", CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Put);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/onboarding/steps/Commercial%20Proposal/complete");
        captured.Content.Should().BeNull();
    }

    /// <summary>
    /// Verifies that complete-step propagates unsuccessful Prospect responses.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenProspectRejectsCompletion_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.CompleteStepAsync(42, "Unknown", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that GetProspectAccountAsync calls the Prospect account details endpoint with signatory.
    /// </summary>
    [Fact]
    public async Task GetProspectAccountAsync_WhenProspectExists_ReturnsAccount()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                prospectId = 10,
                accountId = 42,
                email = "signatory@test.fr",
                firstName = "Jean",
                lastName = "Dupont"
            }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.GetProspectAccountAsync(10, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ProspectId.Should().Be(10);
        result.AccountId.Should().Be(42);
        result.Email.Should().Be("signatory@test.fr");
        result.FirstName.Should().Be("Jean");
        result.LastName.Should().Be("Dupont");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/10/with-signatory");
    }

    /// <summary>
    /// Verifies that GetProspectAccountAsync returns null when Prospect returns not found.
    /// </summary>
    [Fact]
    public async Task GetProspectAccountAsync_WhenProspectDoesNotExist_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetProspectAccountAsync(10, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GetProspectAccountAsync throws when the successful response is empty.
    /// </summary>
    [Fact]
    public async Task GetProspectAccountAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((ProspectAccountResponse?)null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetProspectAccountAsync(10, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that signatory checks call the dedicated Prospect endpoint.
    /// </summary>
    [Fact]
    public async Task IsProspectSignatoryAsync_WhenProspectReturnsTrue_ReturnsTrue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(true, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.IsProspectSignatoryAsync(42, 7, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/signatories/7/exists");
    }

    /// <summary>
    /// Verifies that payment method in-progress updates call the Prospect endpoint.
    /// </summary>
    [Fact]
    public async Task MarkPaymentMethodInProgressAsync_WhenProspectAcceptsRequest_IssuesPost()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        await client.MarkPaymentMethodInProgressAsync(42, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/onboarding/42/in-progress");
        captured.Content.Should().BeNull();
    }

    /// <summary>
    /// Verifies that reset-step sends the compatibility request body expected by Prospect.
    /// </summary>
    [Fact]
    public async Task ResetStepAsync_WhenProspectAcceptsReset_IssuesPost()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            bodyJson = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        await client.ResetStepAsync(10, "PAYMENT_METHOD", CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/onboarding/10/reset-step");
        bodyJson.Should().Contain("\"step\":\"PAYMENT_METHOD\"");
    }

    /// <summary>
    /// Verifies that reset-step propagates unsuccessful Prospect responses.
    /// </summary>
    [Fact]
    public async Task ResetStepAsync_WhenProspectRejectsReset_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.ResetStepAsync(10, "PAYMENT_METHOD", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateProspectAsync_SendsCurrentUserHeaderAndFlatPayload()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { prospectId = 42 }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.CreateProspectAsync(BuildRequest(), BuildInpi(), CancellationToken.None);

        result.Should().Be(42);
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");

        using var json = JsonDocument.Parse(bodyJson!);
        var root = json.RootElement;
        root.GetProperty("legalName").GetString().Should().Be("ACME SARL");
        root.GetProperty("siret").GetString().Should().Be("12345678901234");
        root.GetProperty("legalForm").GetString().Should().Be("SARL");
        root.GetProperty("legalStructure").GetString().Should().Be("PERSONNE_MORALE");
        root.GetProperty("nafCode").GetString().Should().Be("6201Z");
        root.GetProperty("address").GetString().Should().Be("1 rue de la Paix");
        root.GetProperty("zipCode").GetString().Should().Be("75002");
        root.GetProperty("city").GetString().Should().Be("Paris");
        root.GetProperty("department").GetString().Should().Be("75");
        root.GetProperty("region").GetString().Should().Be("11");
        root.GetProperty("country").GetString().Should().Be("FR");
        root.GetProperty("caseManagerContactId").GetInt32().Should().Be(100);
        root.GetProperty("accountManagerContactId").GetInt32().Should().Be(200);

        var signatory = root.GetProperty("signatory");
        signatory.GetProperty("contactDepartment").GetString().Should().Be("Direction");
        signatory.TryGetProperty("department", out _).Should().BeFalse();
        signatory.GetProperty("companyRole").GetString().Should().Be("PRESIDENT_GERANT");
    }

    [Fact]
    public async Task UpdateProspectIdsAsync_IssuesPatchWithSignatoryContactIdAndHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.UpdateProspectIdsAsync(42, "AK-123", 10, 20, CancellationToken.None);

        result.Should().Be(FinalizeProspectOutcome.Updated);
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("20");

        using var json = JsonDocument.Parse(bodyJson!);
        var root = json.RootElement;
        root.GetProperty("accountNumber").GetString().Should().Be("AK-123");
        root.GetProperty("accountId").GetInt32().Should().Be(10);
        root.GetProperty("signatoryContactId").GetInt32().Should().Be(20);
        root.TryGetProperty("contactId", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, FinalizeProspectOutcome.SynchronizationPending)]
    [InlineData(HttpStatusCode.NotFound, FinalizeProspectOutcome.ProspectNotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity, FinalizeProspectOutcome.InvalidCreationStatus)]
    public async Task UpdateProspectIdsAsync_WhenProspectReturnsKnownStatus_MapsOutcome(
        HttpStatusCode statusCode,
        FinalizeProspectOutcome expectedOutcome)
    {
        var response = new HttpResponseMessage(statusCode);
        var (client, _) = CreateClient(response);

        var result = await client.UpdateProspectIdsAsync(42, "AK-123", 10, 20, CancellationToken.None);

        result.Should().Be(expectedOutcome);
    }

    /// <summary>
    /// Verifies that unexpected finalize statuses throw.
    /// </summary>
    [Fact]
    public async Task UpdateProspectIdsAsync_WhenProspectReturnsUnexpectedStatus_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.UpdateProspectIdsAsync(42, "AK-123", 10, 20, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Unexpected status 502*");
    }

    [Fact]
    public async Task CreateProspectAsync_UsesRegionCodeFromInpiNotRequest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { prospectId = 1 }, options: CamelCase)
        };
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var inpi = BuildInpi();
        inpi.RegionCode = "84";
        var request = BuildRequest();

        await client.CreateProspectAsync(request, inpi, CancellationToken.None);

        using var json = JsonDocument.Parse(bodyJson!);
        var root = json.RootElement;
        root.GetProperty("region").GetString().Should().Be("84");
        root.TryGetProperty("regionCode", out _).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the role client sends a single batch payload without a CurrentUser header.
    /// </summary>
    [Fact]
    public async Task PrepareCreationResumeAsync_IssuesPatchWithCurrentUserHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.PrepareCreationResumeAsync(42, 100, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/prepare-creation-resume");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, ProspectRoleSynchronizationOutcome.Synchronized)]
    [InlineData(HttpStatusCode.Conflict, ProspectRoleSynchronizationOutcome.SynchronizationPending)]
    [InlineData(HttpStatusCode.NotFound, ProspectRoleSynchronizationOutcome.ProspectNotFound)]
    public async Task GetCreationRoleSynchronizationOutcomeAsync_WhenProspectReturnsKnownStatus_MapsOutcome(
        HttpStatusCode statusCode,
        ProspectRoleSynchronizationOutcome expected)
    {
        var response = new HttpResponseMessage(statusCode);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetCreationRoleSynchronizationOutcomeAsync(42, CancellationToken.None);

        result.Should().Be(expected);
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/creation-role-synchronization");
    }

    /// <summary>
    /// Verifies that unexpected role synchronization statuses throw.
    /// </summary>
    [Fact]
    public async Task GetCreationRoleSynchronizationOutcomeAsync_WhenProspectReturnsUnexpectedStatus_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetCreationRoleSynchronizationOutcomeAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Unexpected status 502*");
    }

    [Fact]
    public async Task UpdateCreationProgressAsync_IssuesPatchWithHeaderAndBody()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.UpdateCreationProgressAsync(
            42,
            100,
            new UpdateProspectCreationProgressRequest
            {
                CompletedMilestone = ProspectCreationMilestone.RydgeAccountCreated,
                AkuiteoAccountNumber = "AK-123",
                PendingAccountId = 10,
                PendingSignatoryContactId = 20
            },
            CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/creation-progress");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");

        using var json = JsonDocument.Parse(bodyJson!);
        json.RootElement.GetProperty("completedMilestone").GetString().Should().Be("RydgeAccountCreated");
        json.RootElement.GetProperty("akuiteoAccountNumber").GetString().Should().Be("AK-123");
        json.RootElement.GetProperty("pendingAccountId").GetInt32().Should().Be(10);
        json.RootElement.GetProperty("pendingSignatoryContactId").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task UpdateCreationProgressAsync_WhenProspectIsMissing_DoesNotThrow()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.UpdateCreationProgressAsync(
            42,
            100,
            new UpdateProspectCreationProgressRequest { CompletedMilestone = ProspectCreationMilestone.AkuiteoCustomerCreated },
            CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that beneficiary persistence calls the dedicated Prospect endpoint.
    /// </summary>
    [Fact]
    public async Task PersistBeneficiariesAsync_IssuesPostToProspectEndpoint()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.PersistBeneficiariesAsync(42, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/beneficiaries");
    }

    /// <summary>
    /// Verifies that a missing prospect is returned as a false persistence result.
    /// </summary>
    [Fact]
    public async Task PersistBeneficiariesAsync_WhenProspectIsMissing_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.PersistBeneficiariesAsync(42, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkProspectCreationFailedAsync_IssuesPatchWithCurrentUserHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.MarkProspectCreationFailedAsync(
            42,
            100,
            new MarkProspectCreationFailedRequest
            {
                CompletedMilestone = ProspectCreationMilestone.RydgeContactCreated,
                AkuiteoAccountNumber = "AK-123",
                PendingAccountId = 10,
                PendingSignatoryContactId = 20
            },
            CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/creation-failure");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");

        using var json = JsonDocument.Parse(bodyJson!);
        json.RootElement.GetProperty("completedMilestone").GetString().Should().Be("RydgeContactCreated");
        json.RootElement.GetProperty("akuiteoAccountNumber").GetString().Should().Be("AK-123");
        json.RootElement.GetProperty("pendingAccountId").GetInt32().Should().Be(10);
        json.RootElement.GetProperty("pendingSignatoryContactId").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task MarkProspectCreationFailedAsync_WhenProspectIsMissing_DoesNotThrow()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.MarkProspectCreationFailedAsync(
            42,
            100,
            new MarkProspectCreationFailedRequest { CompletedMilestone = ProspectCreationMilestone.RydgeContactCreated },
            CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that GetProspectIdByAccountIdAsync sends the correct HTTP request and returns the prospect identifier.
    /// </summary>
    [Fact]
    public async Task GetProspectIdByAccountIdAsync_WhenProspectFound_ReturnsProspectId()
    {
        const int accountId = 42;
        const int prospectId = 123;
        var responsePayload = new { prospectId };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responsePayload, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.GetProspectIdByAccountIdAsync(accountId, CancellationToken.None);

        result.Should().Be(prospectId);
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/account/42");
    }

    /// <summary>
    /// Verifies that GetProspectIdByAccountIdAsync returns null when no prospect is linked to the account.
    /// </summary>
    [Fact]
    public async Task GetProspectIdByAccountIdAsync_WhenNotFound_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetProspectIdByAccountIdAsync(999, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAkuiteoAccountNumberByProspectIdAsync_WhenFound_ReturnsAccountNumber()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accountNumber = "AK-999" }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetAkuiteoAccountNumberByProspectIdAsync(42, CancellationToken.None);

        result.Should().Be("AK-999");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42/akuiteo-account-number");
    }

    [Fact]
    public async Task GetAkuiteoAccountNumberByProspectIdAsync_WhenNotFound_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetAkuiteoAccountNumberByProspectIdAsync(42, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCommercialProposalEligibilityAsync_WhenFound_ReturnsEligibility()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { canSend = true, alreadySent = false }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetCommercialProposalEligibilityAsync(42, 100, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CanSend.Should().BeTrue();
        result.AlreadySent.Should().BeFalse();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/accounts/42/commercial-proposal/eligibility");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
    }

    [Fact]
    public async Task GetCommercialProposalEligibilityAsync_WhenNotFound_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetCommercialProposalEligibilityAsync(42, 100, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SendCommercialProposalAsync_SendsMultipartRequestWithCurrentUserHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("proposal.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));

        await client.SendCommercialProposalAsync(42, 100, "collab@test.fr", file.Object, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/accounts/42/commercial-proposal");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
        captured.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be("collab@test.fr");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
    }

    [Fact]
    public async Task SendCommercialProposalAsync_WhenProspectRejectsRequest_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("proposal.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        Func<Task> act = () => client.SendCommercialProposalAsync(42, 100, null, file.Object, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetEngagementLetterEligibilityAsync_WhenFound_ReturnsEligibility()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { canSend = true, alreadySent = false }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.GetEngagementLetterEligibilityAsync(42, 100, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CanSend.Should().BeTrue();
        result.AlreadySent.Should().BeFalse();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/accounts/42/engagement-letter/eligibility");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
    }

    [Fact]
    public async Task GetEngagementLetterEligibilityAsync_WhenNotFound_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetEngagementLetterEligibilityAsync(42, 100, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SendEngagementLetterAsync_SendsMultipartRequestWithCurrentUserHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("letter.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));

        await client.SendEngagementLetterAsync(42, 100, "client@test.fr", file.Object, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/accounts/42/engagement-letter");
        captured.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
        captured.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be("client@test.fr");
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenContactEmailIsNull_DoesNotSendContactEmailHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("letter.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        await client.SendEngagementLetterAsync(42, 100, null, file.Object, CancellationToken.None);

        captured!.Headers.Contains("ContactEmail").Should().BeFalse();
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenProspectRejectsRequest_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("letter.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        Func<Task> act = () => client.SendEngagementLetterAsync(42, 100, null, file.Object, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    #region GetAccountIdByProspectIdAsync Tests

    /// <summary>
    /// Verifies that GetAccountIdByProspectIdAsync calls the correct route and returns the account ID.
    /// </summary>
    [Fact]
    public async Task GetAccountIdByProspectIdAsync_WhenProspectExists_ReturnsAccountId()
    {
        const int prospectId = 42;
        const int accountId = 999;
        var responsePayload = new
        {
            prospectId,
            accountId,
            accountNumber = "ACC-999",
            accountType = "PROSPECT",
            legalName = "Test Company",
            siret = "12345678901234",
            legalForm = "SAS"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responsePayload, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, request => captured = request);

        var result = await client.GetAccountIdByProspectIdAsync(prospectId, CancellationToken.None);

        result.Should().Be(accountId);
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42");
    }

    /// <summary>
    /// Verifies that GetAccountIdByProspectIdAsync returns null when prospect does not exist.
    /// </summary>
    [Fact]
    public async Task GetAccountIdByProspectIdAsync_WhenProspectNotFound_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetAccountIdByProspectIdAsync(42, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GetAccountIdByProspectIdAsync throws when response is empty.
    /// </summary>
    [Fact]
    public async Task GetAccountIdByProspectIdAsync_WhenResponseIsEmpty_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create((object?)null, options: CamelCase)
        };
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.GetAccountIdByProspectIdAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    #endregion

    #region GetDocumentRequirementsAsync Tests

    /// <summary>
    /// Verifies that GetDocumentRequirementsAsync calls accountId resolution and returns requirements.
    /// </summary>
    [Fact]
    public async Task GetDocumentRequirementsAsync_WhenProspectExists_ReturnsDocumentRequirements()
    {
        const int prospectId = 42;
        const int accountId = 999;

        // Set up two sequential responses: first for accountId resolution, second for requirements
        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedAccountIdRequest = null;
        HttpRequestMessage? capturedRequirementsRequest = null;

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (handlerCalls == 0)
                {
                    capturedAccountIdRequest = req;
                }
                else
                {
                    capturedRequirementsRequest = req;
                }
            })
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    // First call: return accountId
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Second call: return document requirements
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        documents = new[]
                        {
                            new { type = "KBIS", maxFiles = 1, documents = new object[] { } },
                            new { type = "STATUTS", maxFiles = 1, documents = new object[] { } }
                        }
                    }, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var result = await client.GetDocumentRequirementsAsync(prospectId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Documents.Should().HaveCount(2);
        result.Documents[0].Type.Should().Be("KBIS");
        result.Documents[0].MaxFiles.Should().Be(1);
        result.Documents[1].Type.Should().Be("STATUTS");

        // Verify first call was to get accountId
        capturedAccountIdRequest!.Method.Should().Be(HttpMethod.Get);
        capturedAccountIdRequest.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42");

        // Verify second call was to get requirements with accountId
        capturedRequirementsRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequirementsRequest.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/999/supporting-documents");
    }

    /// <summary>
    /// Verifies that GetDocumentRequirementsAsync returns null when prospect is not found during accountId resolution.
    /// </summary>
    [Fact]
    public async Task GetDocumentRequirementsAsync_WhenProspectNotFoundDuringResolution_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var result = await client.GetDocumentRequirementsAsync(42, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GetDocumentRequirementsAsync returns null when requirements endpoint returns 404.
    /// </summary>
    [Fact]
    public async Task GetDocumentRequirementsAsync_WhenRequirementsNotFound_ReturnsNull()
    {
        const int prospectId = 42;
        const int accountId = 999;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    // First call: return accountId
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Second call: return 404
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var result = await client.GetDocumentRequirementsAsync(prospectId, CancellationToken.None);

        result.Should().BeNull();
    }

    #endregion

    #region UploadSupportingDocumentAsync Tests

    private const string ContactEmail = "collaborator@test.fr";

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync successfully uploads a file and returns the document ID.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenUploadSucceeds_ReturnsSuccessWithDocumentId()
    {
        const int prospectId = 42;
        const int accountId = 999;
        const int currentUserId = 100;
        const int documentId = 555;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedAccountIdRequest = null;
        HttpRequestMessage? capturedUploadRequest = null;

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (handlerCalls == 0)
                {
                    capturedAccountIdRequest = req;
                }
                else
                {
                    capturedUploadRequest = req;
                }
            })
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    // First call: return accountId
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Second call: return upload success
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new { documentId }, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("kbis.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(prospectId, currentUserId, ContactEmail, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.Success);
        result.DocumentId.Should().Be(documentId);

        // Verify accountId resolution call
        capturedAccountIdRequest!.Method.Should().Be(HttpMethod.Get);
        capturedAccountIdRequest.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/42");

        // Verify upload call
        capturedUploadRequest!.Method.Should().Be(HttpMethod.Post);
        capturedUploadRequest.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/prospects/999/supporting-documents");
        capturedUploadRequest.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
        capturedUploadRequest.Headers.Should().Contain(h => h.Key == "CurrentUser" && h.Value.Contains("100"));
        capturedUploadRequest.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be(ContactEmail);
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync constructs multipart form data correctly.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_ConstructsMultipartFormDataCorrectly()
    {
        const int prospectId = 42;
        const int accountId = 999;
        const int currentUserId = 100;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        MultipartFormDataContent? capturedFormData = null;

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                if (handlerCalls == 1 && req.Content is MultipartFormDataContent multipart)
                {
                    // Clone the content for verification (multipart can only be read once)
                    capturedFormData = multipart;
                }
            })
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new { documentId = 555 }, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("statuts.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1, 2, 3, 4, 5]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "STATUTS",
            File = file.Object
        };

        await client.UploadSupportingDocumentAsync(prospectId, currentUserId, ContactEmail, request, CancellationToken.None);

        capturedFormData.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync returns ProspectNotFound when accountId resolution fails.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenProspectNotFoundDuringResolution_ReturnsProspectNotFound()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var (client, _) = CreateClient(response);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("kbis.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(42, 100, null, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ProspectNotFound);
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync returns ProspectNotFound when upload endpoint returns 404.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenUploadEndpointReturns404_ReturnsProspectNotFound()
    {
        const int prospectId = 42;
        const int accountId = 999;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Upload returns 404
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("kbis.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(prospectId, 100, null, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ProspectNotFound);
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync returns ValidationError when upload returns 400.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenValidationFails_ReturnsValidationError()
    {
        const int prospectId = 42;
        const int accountId = 999;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Upload returns 400 with validation error
                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "One or more validation errors occurred.",
                    status = 400,
                    detail = "Invalid document type",
                    field = "documentType",
                    errorCode = "INVALID_DOCUMENT_TYPE"
                };

                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(problemDetails, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("invalid.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "INVALID_TYPE",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(prospectId, 100, null, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ValidationError);
        result.FieldName.Should().Be("documentType");
        result.ErrorCode.Should().Be("INVALID_DOCUMENT_TYPE");
        result.ErrorMessage.Should().Be("Invalid document type");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync preserves validation details when ProblemDetails extensions are nested.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenValidationProblemExtensionsAreNested_ReturnsValidationErrorDetails()
    {
        const int prospectId = 42;
        const int accountId = 999;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                var problemDetails = new
                {
                    title = "One or more validation errors occurred.",
                    status = 400,
                    detail = "La limite de 1 document(s) pour le type 'KBIS' est atteinte.",
                    errors = new Dictionary<string, string[]>
                    {
                        ["documentType"] = ["La limite de 1 document(s) pour le type 'KBIS' est atteinte."]
                    },
                    extensions = new
                    {
                        errorCode = "PRS008",
                        field = "documentType"
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(problemDetails, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("kbis.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(prospectId, 100, null, request, CancellationToken.None);

        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ValidationError);
        result.FieldName.Should().Be("documentType");
        result.ErrorCode.Should().Be("PRS008");
        result.ErrorMessage.Should().Be("La limite de 1 document(s) pour le type 'KBIS' est atteinte.");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync reads validation details from the first model-state error.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenValidationErrorsContainDetails_ReturnsFirstErrorDetails()
    {
        var (client, _) = CreateSupportingDocumentClient(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["ContactEmail"] = ["The ContactEmail header is required."]
                    }
                }, options: CamelCase)
            });

        var result = await client.UploadSupportingDocumentAsync(
            42,
            100,
            null,
            CreateSupportingDocumentRequest(),
            CancellationToken.None);

        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ValidationError);
        result.FieldName.Should().Be("ContactEmail");
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Be("The ContactEmail header is required.");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync falls back to default validation details when ProblemDetails is empty.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenValidationProblemBodyIsEmpty_ReturnsDefaultValidationError()
    {
        var (client, _) = CreateSupportingDocumentClient(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(string.Empty)
            });

        var result = await client.UploadSupportingDocumentAsync(
            42,
            100,
            null,
            CreateSupportingDocumentRequest(),
            CancellationToken.None);

        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.ValidationError);
        result.FieldName.Should().Be(ProblemDetailsKeys.DefaultField);
        result.ErrorCode.Should().Be(ProblemDetailsKeys.DefaultErrorCode);
        result.ErrorMessage.Should().Be(ProblemDetailsKeys.DefaultValidationMessage);
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync falls back to default file-too-large details when ProblemDetails is invalid JSON.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenFileTooLargeProblemBodyIsInvalidJson_ReturnsDefaultFileTooLargeError()
    {
        var (client, _) = CreateSupportingDocumentClient(
            new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
            {
                Content = new StringContent("{invalid-json")
            });

        var result = await client.UploadSupportingDocumentAsync(
            42,
            100,
            null,
            CreateSupportingDocumentRequest(),
            CancellationToken.None);

        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.FileTooLarge);
        result.ErrorCode.Should().Be(ProblemDetailsKeys.DefaultFileTooLargeCode);
        result.ErrorMessage.Should().Be(ProblemDetailsKeys.DefaultFileTooLargeMessage);
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync rejects a successful response without a valid document identifier.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenCreatedResponseHasInvalidDocumentId_ThrowsHttpRequestException()
    {
        var (client, _) = CreateSupportingDocumentClient(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new { documentId = 0 }, options: CamelCase)
            });

        Func<Task> act = () => client.UploadSupportingDocumentAsync(
            42,
            100,
            ContactEmail,
            CreateSupportingDocumentRequest(),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Prospect API returned 201 but response body missing or invalid documentId");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync does not send a contact email header when the email is blank.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenContactEmailIsBlank_DoesNotSendContactEmailHeader()
    {
        HttpRequestMessage? capturedUploadRequest = null;
        var (client, _) = CreateSupportingDocumentClient(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new { documentId = 555 }, options: CamelCase)
            },
            request => capturedUploadRequest = request);

        var result = await client.UploadSupportingDocumentAsync(
            42,
            100,
            " ",
            CreateSupportingDocumentRequest(),
            CancellationToken.None);

        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.Success);
        capturedUploadRequest!.Headers.Contains("ContactEmail").Should().BeFalse();
        capturedUploadRequest.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be("100");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync returns FileTooLarge when upload returns 413.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenFileTooLarge_ReturnsFileTooLarge()
    {
        const int prospectId = 42;
        const int accountId = 999;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                // Upload returns 413
                var problemDetails = new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.11",
                    title = "Payload Too Large",
                    status = 413,
                    detail = "File size exceeds maximum allowed size of 10MB",
                    errorCode = "FILE_TOO_LARGE"
                };

                return new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
                {
                    Content = JsonContent.Create(problemDetails, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("huge.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[15 * 1024 * 1024])); // 15MB

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        var result = await client.UploadSupportingDocumentAsync(prospectId, 100, null, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.Outcome.Should().Be(UploadSupportingDocumentOutcome.FileTooLarge);
        result.ErrorCode.Should().Be("FILE_TOO_LARGE");
        result.ErrorMessage.Should().Be("File size exceeds maximum allowed size of 10MB");
    }

    /// <summary>
    /// Verifies that UploadSupportingDocumentAsync includes currentUserId as query parameter.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_IncludesCurrentUserIdInQueryString()
    {
        const int prospectId = 42;
        const int accountId = 999;
        const int currentUserId = 777;

        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedUploadRequest = null;

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (handlerCalls == 1)
                {
                    capturedUploadRequest = req;
                }
            })
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId,
                            accountId,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(new { documentId = 555 }, options: CamelCase)
                };
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        var client = new ProspectApiClient(httpClient, logger.Object);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("doc.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };

        await client.UploadSupportingDocumentAsync(prospectId, currentUserId, ContactEmail, request, CancellationToken.None);

        capturedUploadRequest!.Headers.Should().Contain(h => h.Key == "CurrentUser" && h.Value.Contains("777"));
    }

    #endregion

    /// <summary>
    /// Creates a Prospect client for supporting document upload tests.
    /// </summary>
    /// <param name="uploadResponse">The upload endpoint response.</param>
    /// <param name="captureUploadRequest">An optional callback for the upload request.</param>
    /// <returns>The client and mocked message handler.</returns>
    private static (ProspectApiClient Client, Mock<HttpMessageHandler> Handler) CreateSupportingDocumentClient(
        HttpResponseMessage uploadResponse,
        Action<HttpRequestMessage>? captureUploadRequest = null)
    {
        var handlerCalls = 0;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (handlerCalls == 1)
                {
                    captureUploadRequest?.Invoke(req);
                }
            })
            .ReturnsAsync(() =>
            {
                handlerCalls++;
                if (handlerCalls == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new
                        {
                            prospectId = 42,
                            accountId = 999,
                            accountNumber = "ACC-999",
                            accountType = "PROSPECT",
                            legalName = "Test Company",
                            siret = "12345678901234",
                            legalForm = "SAS"
                        }, options: CamelCase)
                    };
                }

                return uploadResponse;
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://prospect.test/")
        };
        var logger = new Mock<ILogger<ProspectApiClient>>();
        return (new ProspectApiClient(httpClient, logger.Object), handler);
    }

    /// <summary>
    /// Creates a valid supporting document upload request.
    /// </summary>
    /// <returns>The upload request.</returns>
    private static UploadSupportingDocumentRequest CreateSupportingDocumentRequest()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("kbis.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([1]));

        return new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = file.Object
        };
    }

}

