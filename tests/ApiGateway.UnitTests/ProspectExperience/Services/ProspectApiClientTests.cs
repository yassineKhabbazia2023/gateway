using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.ProspectExperience.Models.Contracts;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
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
        ShareCapital = 10000
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
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        await client.UpdateProspectIdsAsync(42, "AK-123", 10, 20, CancellationToken.None);

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

    /// <summary>
    /// Verifies that the role client sends a single batch payload without a CurrentUser header.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_IssuesPostWithBatchRolePayloadAndNoCurrentUserHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var (client, _) = CreateClient(response, req =>
        {
            captured = req;
            bodyJson = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var requests = new[]
        {
            new CreateRoleAssignmentRequest
            {
                ContactId = 20,
                AccountId = 10,
                IsSignatory = true
            },
            new CreateRoleAssignmentRequest
            {
                ContactId = 30,
                AccountId = 10,
                IsSignatory = false
            }
        };

        await client.CreateRoleAsync(requests, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://prospect.test/api/roles");
        captured.Headers.Contains("CurrentUser").Should().BeFalse();

        using var json = JsonDocument.Parse(bodyJson!);
        var root = json.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array);
        root.GetArrayLength().Should().Be(2);
        root[0].GetProperty("contactId").GetInt32().Should().Be(20);
        root[0].GetProperty("accountId").GetInt32().Should().Be(10);
        root[0].GetProperty("isSignatory").GetBoolean().Should().BeTrue();
        root[1].GetProperty("contactId").GetInt32().Should().Be(30);
        root[1].GetProperty("accountId").GetInt32().Should().Be(10);
        root[1].GetProperty("isSignatory").GetBoolean().Should().BeFalse();
    }
}
