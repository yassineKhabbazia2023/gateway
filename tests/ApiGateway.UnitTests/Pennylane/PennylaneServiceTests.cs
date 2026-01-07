using System.Net;
using System.Net.Http.Json;
using ApiGateway.Account.Constants;
using ApiGateway.Offer.Model;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq.Protected;

namespace ApiGateway.UnitTests.Pennylane;

public class PennylaneServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockPennylaneHttpMessageHandler;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<PennylaneService>> _mockLogger;
    private readonly PennylaneService _pennylaneService;

    public PennylaneServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockPennylaneHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<PennylaneService>>();

        var pennylaneHttpClient = new HttpClient(_mockPennylaneHttpMessageHandler.Object);
        pennylaneHttpClient.BaseAddress = new Uri("http://local.pennylane/");

        _mockHttpClientFactory
            .Setup(factory => factory.CreateClient("PennylaneClient"))
            .Returns(pennylaneHttpClient);

        _pennylaneService = new PennylaneService(
            _mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    #region ShouldCreateCompanyForOffer Tests

    [Fact]
    public void ShouldCreateCompanyForOffer_WhenOfferIdMatches_ShouldReturnTrue()
    {
        // Arrange
        var offerId = 999;
        _mockConfiguration.Setup(c => c["PennylaneOfferId"]).Returns("999");

        // Act
        var result = _pennylaneService.ShouldCreateCompanyForOffer(offerId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCreateCompanyForOffer_WhenOfferIdDoesNotMatch_ShouldReturnFalse()
    {
        // Arrange
        var offerId = 888;
        _mockConfiguration.Setup(c => c["PennylaneOfferId"]).Returns("999");

        // Act
        var result = _pennylaneService.ShouldCreateCompanyForOffer(offerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCreateCompanyForOffer_WhenConfigNotSet_ShouldReturnFalse()
    {
        // Arrange
        var offerId = 999;
        _mockConfiguration.Setup(c => c["PennylaneOfferId"]).Returns((string?)null);

        // Act
        var result = _pennylaneService.ShouldCreateCompanyForOffer(offerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCreateCompanyForOffer_WhenConfigIsEmpty_ShouldReturnFalse()
    {
        // Arrange
        var offerId = 999;
        _mockConfiguration.Setup(c => c["PennylaneOfferId"]).Returns("");

        // Act
        var result = _pennylaneService.ShouldCreateCompanyForOffer(offerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCreateCompanyForOffer_WhenConfigIsInvalidNumber_ShouldReturnFalse()
    {
        // Arrange
        var offerId = 999;
        _mockConfiguration.Setup(c => c["PennylaneOfferId"]).Returns("invalid");

        // Act
        var result = _pennylaneService.ShouldCreateCompanyForOffer(offerId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateCompanyAsync Tests

    [Fact]
    public async Task CreateCompanyAsync_WithValidRequest_ShouldReturnCompanyResult()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var expectedResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(expectedResult)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString().Contains("/api/pennylane/companies/onboarding")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Company.Id.Should().Be("ACC123");
        result.Company.Name.Should().Be("Test Company");
        result.Status.Should().Be("created");
    }

    [Fact]
    public async Task CreateCompanyAsync_ShouldCallCorrectEndpoint()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(companyResult)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest!.RequestUri!.ToString().Should().Contain("/api/pennylane/companies/onboarding");
    }

    [Fact]
    public async Task CreateCompanyAsync_ShouldSerializeRequestCorrectly()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20, 30 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(companyResult)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();

        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain("\"accountId\":123");
        content.Should().Contain("\"contacts\":[10,20,30]");
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenHttpError_ShouldThrowException()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("Bad request error")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*BadRequest*");
    }

    [Fact]
    public async Task CreateCompanyAsync_When500Error_ShouldThrowException()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("Internal server error")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*InternalServerError*");
    }

    [Fact]
    public async Task CreateCompanyAsync_WhenResponseIsNull_ShouldThrowException()
    {
        // Arrange
        var request = new CreateCompanyRequest
        {
            AccountId = 123,
            Contacts = new List<int> { 10, 20 },
            NotYetRegistered = false,
            RegistrationNumber = "REGN01",
            AccountingType = AccountConstants.Engagemment,
            CountryCode = "FR"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create<CreateCompanyResult?>(null)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.CreateCompanyAsync(request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    #endregion

    #region GrantPennylaneAccessAsync Tests

    [Fact]
    public async Task GrantPennylaneAccessAsync_WithValidRequest_ShouldReturnResult()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var accessResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Created,
            Message = "ok",
            ContactId = request.ContactId,
            AccountId = request.AccountId
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(accessResult)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.GrantPennylaneAccessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PennylaneAccessStatuses.Created);
        result.AccountId.Should().Be(request.AccountId);
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenHttpError_ShouldThrow()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("bad")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.GrantPennylaneAccessAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*BadRequest*");
    }

    [Fact]
    public async Task GrantPennylaneAccessAsync_WhenResponseNull_ShouldThrow()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create<GrantPennylaneAccessResult?>(null)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.GrantPennylaneAccessAsync(request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    #endregion

    #region UpdatePennylaneRoleAsync Tests

    [Fact]
    public async Task UpdatePennylaneRoleAsync_WithValidRequest_ShouldReturnResult()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var roleUpdateResponse = $"Role assigned to contact {request.ContactId} on account {request.AccountId}.";

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(roleUpdateResponse)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.UpdatePennylaneRoleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PennylaneAccessStatuses.ExistingUserAccessGranted);
        result.AccountId.Should().Be(request.AccountId);
        result.Message.Should().Be(roleUpdateResponse);
        result.Role.Should().Be(request.Role);
    }

    [Fact]
    public async Task UpdatePennylaneRoleAsync_WhenHttpError_ShouldThrow()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("bad")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.UpdatePennylaneRoleAsync(request);

        // Assert
        await action.Should().ThrowAsync<PennylaneApiException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePennylaneRoleAsync_WithEmptyResponse_ShouldReturnDefaultMessage()
    {
        // Arrange
        var request = new PennylaneAuthorizationRequest
        {
            AccountId = 123,
            ContactId = 456,
            Role = "role"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(string.Empty)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.UpdatePennylaneRoleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PennylaneAccessStatuses.ExistingUserAccessGranted);
        result.Message.Should().Be("Role updated");
        result.ContactId.Should().Be(request.ContactId);
        result.AccountId.Should().Be(request.AccountId);
    }

    #endregion

    #region RevokePennylaneAccessAsync Tests

    [Fact]
    public async Task RevokePennylaneAccessAsync_WithValidRequest_ShouldReturnResult()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var revokeResult = new RevokePennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Revoked,
            Message = "Access revoked",
            ContactId = request.ContactId,
            AccountId = request.AccountId,
            PennylaneUserId = "pl-user-123",
            PennylaneCompanyId = "pl-company-456"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(revokeResult)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PennylaneAccessStatuses.Revoked);
        result.AccountId.Should().Be(request.AccountId);
        result.ContactId.Should().Be(request.ContactId);
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_ShouldCallCorrectEndpoint()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var revokeResult = new RevokePennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Revoked,
            Message = "Access revoked",
            ContactId = request.ContactId,
            AccountId = request.AccountId
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(revokeResult)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest!.RequestUri!.ToString().Should().Contain("/api/pennylane/role/revoke");
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenUserNotFound_ShouldReturnUserNotFoundStatus()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var revokeResult = new RevokePennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.UserNotFound,
            Message = "User not found in company",
            ContactId = request.ContactId,
            AccountId = request.AccountId
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(revokeResult)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PennylaneAccessStatuses.UserNotFound);
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenHttpError_ShouldThrow()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("bad request")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*BadRequest*");
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_WhenResponseNull_ShouldThrow()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create<RevokePennylaneAccessResult?>(null)
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    [Fact]
    public async Task RevokePennylaneAccessAsync_When500Error_ShouldThrowException()
    {
        // Arrange
        var request = new RevokeAccessRequest
        {
            AccountId = 123,
            ContactId = 456
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("Internal server error")
        };

        _mockPennylaneHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _pennylaneService.RevokePennylaneAccessAsync(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*InternalServerError*");
    }

    #endregion
}
