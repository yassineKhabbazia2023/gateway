using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
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
    /// Verifies that complete-step calls the generic Prospect endpoint with an escaped step name.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenProspectAcceptsCompletion_IssuesPut()
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
}
