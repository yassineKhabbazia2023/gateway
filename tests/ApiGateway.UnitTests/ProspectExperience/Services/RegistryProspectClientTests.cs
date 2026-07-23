using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

public class RegistryProspectClientTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static (RegistryProspectClient client, Mock<HttpMessageHandler> handler) CreateClient(HttpResponseMessage response, Action<HttpRequestMessage>? capture = null)
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
            BaseAddress = new Uri("https://registry.test/")
        };
        return (new RegistryProspectClient(httpClient, NullLogger<RegistryProspectClient>.Instance), handler);
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
        Signatory = new SignatoryDto
        {
            Title = "M",
            LastName = "Dupont",
            FirstName = "Jean",
            JobTitle = "Dirigeant",
            Department = "Direction",
            CompanyRole = "PRESIDENT_GERANT",
            ContactTypes = ["SIGNATAIRE"],
            Email = "jean.dupont@test.fr",
            MobilePhone = "+33612345678"
        }
    };

    private static InpiCompanyInfo BuildInpi(string siret = "12345678901234") => new()
    {
        Siret = siret,
        Siren = siret[..9],
        LegalName = "ACME SARL",
        NafCode = "6201Z",
        Address = "10 rue des Lilas",
        ZipCode = "75001",
        City = "Paris",
        LegalForm = "SARL",
        ShareCapital = 10000,
        RegionCode = "11"
    };

    [Fact]
    public async Task SiretExistsInAkuiteoAsync_WhenStatusIsOk_ReturnsTrue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        var (client, _) = CreateClient(response, req => captured = req);

        var result = await client.SiretExistsInAkuiteoAsync("12345678901234", CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.PathAndQuery.Should().Be("/api/prospects/check-eligibility?siret=12345678901234");
    }

    [Fact]
    public async Task SiretExistsInAkuiteoAsync_WhenStatusIsAccepted_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        var (client, _) = CreateClient(response);

        var result = await client.SiretExistsInAkuiteoAsync("12345678901234", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SiretExistsInAkuiteoAsync_WhenStatusIsUnexpected_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var (client, _) = CreateClient(response);

        Func<Task> act = () => client.SiretExistsInAkuiteoAsync("x", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateAkuiteoCustomerAsync_PostsFullPayloadAndReturnsAccountNumber()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { accountNumber = "AK-001" }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        string? body = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.CreateAkuiteoCustomerAsync(BuildRequest(), BuildInpi(), CancellationToken.None);

        result.AccountNumber.Should().Be("AK-001");
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://registry.test/api/akuiteo/customers");
        body.Should().NotBeNull();

        using var json = JsonDocument.Parse(body!);
        var root = json.RootElement;
        root.GetProperty("siret").GetString().Should().Be("12345678901234");
        root.GetProperty("siren").GetString().Should().Be("123456789");
        root.GetProperty("legalName").GetString().Should().Be("ACME SARL");
        root.GetProperty("legalStructure").GetString().Should().Be("PERSONNE_MORALE");
        root.GetProperty("legalForm").GetString().Should().Be("SARL");
        root.GetProperty("nafCode").GetString().Should().Be("6201Z");
        root.GetProperty("address").GetString().Should().Be("10 rue des Lilas");
        root.GetProperty("zipCode").GetString().Should().Be("75001");
        root.GetProperty("city").GetString().Should().Be("Paris");
        root.GetProperty("departmentCode").GetString().Should().Be("75");
        root.GetProperty("regionCode").GetString().Should().Be("11");
        root.GetProperty("countryCode").GetString().Should().Be("FR");
        root.GetProperty("caseManagerContactId").GetInt32().Should().Be(100);
        root.GetProperty("accountManagerContactId").GetInt32().Should().Be(200);
    }

    [Fact]
    public async Task CreateAkuiteoCustomerAsync_UsesRegionCodeFromInpiNotRequest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { accountNumber = "AK-002" }, options: CamelCase)
        };
        string? body = null;
        var (client, _) = CreateClient(response, req =>
        {
            body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var inpi = BuildInpi();
        inpi.RegionCode = "84";
        var request = BuildRequest();

        await client.CreateAkuiteoCustomerAsync(request, inpi, CancellationToken.None);

        using var json = JsonDocument.Parse(body!);
        json.RootElement.GetProperty("regionCode").GetString().Should().Be("84");
    }

    [Fact]
    public async Task CreateAkuiteoContactAsync_PostsPayloadWithContactDepartmentAndTypedFlags()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { contactId = "C-123" }, options: CamelCase)
        };
        HttpRequestMessage? captured = null;
        string? body = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var signatory = new SignatoryDto
        {
            Title = "M.",
            LastName = "Dupont",
            FirstName = "Jean",
            JobTitle = "Dir",
            Department = "Direction",
            CompanyRole = "Gérant",
            ContactTypes = ["COFFRE_FORT_NUMERIQUE", "SIGNATAIRE"],
            Email = "jd@t.fr",
            MobilePhone = "0612345678"
        };

        await client.CreateAkuiteoContactAsync("AK-001", signatory, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://registry.test/api/akuiteo/contacts");
        body.Should().NotBeNull();

        using var json = JsonDocument.Parse(body!);
        var root = json.RootElement;
        root.GetProperty("accountNumber").GetString().Should().Be("AK-001");
        root.GetProperty("contactDepartment").GetString().Should().Be("Direction");
        root.TryGetProperty("department", out _).Should().BeFalse();

        var types = root.GetProperty("contactTypes");
        types.GetProperty("isDigitalVaultContact").GetBoolean().Should().BeTrue();
        types.GetProperty("isDebtCollectionContact").GetBoolean().Should().BeFalse();
        types.GetProperty("isMandateSignatory").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Verifies that document uploads are sent as multipart form-data using the Registry contract.
    /// </summary>
    [Fact]
    public async Task UploadAkuiteoDocumentAsync_WhenRegistryCreatesDocument_ReturnsTrueAndSendsMultipartDocument()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        HttpRequestMessage? captured = null;
        string? body = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var result = await client.UploadAkuiteoDocumentAsync(
            "AK/001",
            new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"),
            CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://registry.test/api/akuiteo/account/AK%2F001/documents");
        captured.Content.Should().BeOfType<MultipartFormDataContent>();
        body.Should().Contain("name=document");
        body.Should().Contain("filename=PASSEPORT_DUPONT_Jean");
        body.Should().Contain("Content-Type: application/pdf");
    }

    /// <summary>
    /// Verifies that non-created Registry document upload responses are reported as failures.
    /// </summary>
    [Fact]
    public async Task UploadAkuiteoDocumentAsync_WhenRegistryDoesNotCreateDocument_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"title":"invalid document"}""")
        };
        var (client, _) = CreateClient(response);

        var result = await client.UploadAkuiteoDocumentAsync(
            "AK-001",
            new ProspectDocumentContentResponse([1], "image/png", "CNI_Recto_DUPONT_Jean"),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that extracted banking information is posted as the Registry collection contract.
    /// </summary>
    [Fact]
    public async Task UpdateAkuiteoBankingInformationAsync_WhenRegistrySucceeds_SendsFullPayload()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        string? body = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });
        var bankingInformation = new AkuiteoBankingInformationRequest
        {
            Sepa = new AkuiteoSepaRequest
            {
                BankDetails = new AkuiteoBankDetailsRequest
                {
                    Entity = "30006",
                    Counter = "00001",
                    AccountNumber = "12345678901",
                    Key = "89",
                    Domiciliation = "AGRI"
                },
                Bic = new AkuiteoBicRequest
                {
                    Country = "FR",
                    Bank = "AGRI",
                    Location = "FR",
                    Branch = "PP"
                },
                Iban = new AkuiteoIbanRequest
                {
                    Country = "FR",
                    Key = "76",
                    AccountNumber = "30006000011234567890189"
                }
            },
            Action = "ADD"
        };

        var result = await client.UpdateAkuiteoBankingInformationAsync(
            42,
            bankingInformation,
            CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://registry.test/api/akuiteo/account/42/banking-informations");
        using var payload = JsonDocument.Parse(body!);
        var items = payload.RootElement.EnumerateArray().ToArray();
        items.Should().ContainSingle();
        var item = items[0];
        item.GetProperty("action").GetString().Should().Be("ADD");
        item.GetProperty("noneSepa").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("sepa").GetProperty("bankDetails").GetProperty("entity").GetString().Should().Be("30006");
        item.GetProperty("sepa").GetProperty("bic").GetProperty("bank").GetString().Should().Be("AGRI");
        item.GetProperty("sepa").GetProperty("iban").GetProperty("accountNumber").GetString()
            .Should().Be("30006000011234567890189");
    }

    /// <summary>
    /// Verifies that Registry banking-information failures are reported to the orchestration.
    /// </summary>
    [Fact]
    public async Task UpdateAkuiteoBankingInformationAsync_WhenRegistryFails_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"title":"invalid banking information"}""")
        };
        var (client, _) = CreateClient(response);

        var result = await client.UpdateAkuiteoBankingInformationAsync(
            42,
            new AkuiteoBankingInformationRequest { Sepa = new AkuiteoSepaRequest(), Action = "ADD" },
            CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the account PATCH sends exactly the direct-debit payload required by Akuiteo.
    /// </summary>
    [Fact]
    public async Task PatchAkuiteoAccountPaymentMethodAsync_WhenRegistrySucceeds_SendsExactPayload()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        HttpRequestMessage? captured = null;
        string? body = null;
        var (client, _) = CreateClient(response, request =>
        {
            captured = request;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });
        var request = new AkuiteoAccountPaymentMethodRequest
        {
            ConditionOfPayment = new AkuiteoConditionOfPaymentRequest
            {
                DeadLine = string.Empty,
                Term = string.Empty,
                Day = 0
            },
            MethodOfPayment = "DIRECT_DEBIT"
        };

        var result = await client.PatchAkuiteoAccountPaymentMethodAsync(42, request, CancellationToken.None);

        result.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://registry.test/api/akuiteo/account/42");
        using var payload = JsonDocument.Parse(body!);
        payload.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().Equal("conditionOfPayment", "methodOfPayment");
        var condition = payload.RootElement.GetProperty("conditionOfPayment");
        condition.EnumerateObject().Select(property => property.Name)
            .Should().Equal("deadLine", "term", "day");
        condition.GetProperty("deadLine").GetString().Should().BeEmpty();
        condition.GetProperty("term").GetString().Should().BeEmpty();
        condition.GetProperty("day").GetInt32().Should().Be(0);
        payload.RootElement.GetProperty("methodOfPayment").GetString().Should().Be("DIRECT_DEBIT");
    }

    /// <summary>
    /// Verifies that Registry account-patch failures are reported to the orchestration.
    /// </summary>
    [Fact]
    public async Task PatchAkuiteoAccountPaymentMethodAsync_WhenRegistryFails_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"title":"invalid account patch"}""")
        };
        var (client, _) = CreateClient(response);

        var result = await client.PatchAkuiteoAccountPaymentMethodAsync(
            42,
            new AkuiteoAccountPaymentMethodRequest(),
            CancellationToken.None);

        result.Should().BeFalse();
    }

}
