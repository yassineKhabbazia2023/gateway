using System.Net;
using System.Net.Http.Json;
using ApiGateway.Models;
using ApiGateway.Offer;
using ApiGateway.Offer.Model;
using Moq.Protected;

namespace ApiGateway.UnitTests.Authorization;

public class OfferServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly OfferService _offerService;

    public OfferServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _httpClient.BaseAddress = new Uri("http://local.account/api");

        _offerService = new OfferService(_httpClient);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenResponseUnsuccessful_ReturnsNull()
    {
        var accountId = 193216;
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _offerService.GetSubscriptionsAsync(accountId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_WhenAccountExists_ReturnsSummary()
    {
        var accountId = 193216;
        var subscriptions = new Fixture().Create<SubscriptionStatus[]>();

        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(subscriptions)
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _offerService.GetSubscriptionsAsync(accountId);
        result.Should().BeEquivalentTo(subscriptions);
    }

    [Fact]
    public async Task GetSubscriptionsAsync_WhenBodyIsJsonNull_ReturnsEmpty()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create<SubscriptionStatus[]?>(null)
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetSubscriptionsAsync(193216);

        result.Should().BeEmpty();
    }

    #region CreateSubscriptionAsync Tests

    [Fact]
    public async Task CreateSubscriptionAsync_WithValidRequest_ShouldReturnSubscriptionId()
    {
        // Arrange
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();
        var expectedSubscriptionId = 123456;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(expectedSubscriptionId)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null && req.RequestUri.ToString().Contains("/api/subscription")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        result.Should().Be(expectedSubscriptionId);
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ShouldCallCorrectEndpoint()
    {
        // Arrange
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();
        var subscriptionId = 123;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(subscriptionId)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest!.RequestUri!.ToString().Should().Contain("/api/subscription");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ShouldSerializeRequestCorrectly()
    {
        // Arrange
        var createRequest = new ApiGateway.Offer.Model.CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456,
            PlanId = 789,
            ProductConfigurations = new List<int> { 1, 2, 3 },
            Contacts = new List<int> { 10, 20 },
            Notes = new List<ApiGateway.Offer.Model.Note>
            {
                new ApiGateway.Offer.Model.Note { StepName = "Step1", StepNote = "Note1" }
            },
            Applicant = "John Doe"
        };

        var subscriptionId = 999;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(subscriptionId)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();

        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain("\"accountId\":123");
        content.Should().Contain("\"offerId\":456");
        content.Should().Contain("\"planId\":789");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WhenHttpError_ShouldThrowException()
    {
        // Arrange
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*BadRequest*");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_When404_ShouldThrowNotFoundException()
    {
        // Arrange
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var action = async () => await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*NotFound*");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithContactEmail_ShouldForwardContactEmailHeader()
    {
        // Arrange: l'email doit être forwardé via le header ContactEmail (garde-fou Sérénité côté Offer)
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(123)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _offerService.CreateSubscriptionAsync(createRequest, "user@test.fr");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues("ContactEmail").Should().ContainSingle().Which.Should().Be("user@test.fr");
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithoutContactEmail_ShouldNotSendContactEmailHeader()
    {
        // Arrange
        var createRequest = new Fixture().Create<ApiGateway.Offer.Model.CreateSubscriptionOffer>();

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(123)
        };

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _offerService.CreateSubscriptionAsync(createRequest);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Contains("ContactEmail").Should().BeFalse();
    }

    #endregion

    #region GetAccountIdsWithActiveSubscriptionAsync Tests

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenTooManyAccountIds_ShouldThrowWithoutCallingHttp()
    {
        // 201 ids : au-dela du plafond, on veut une erreur explicite et aucun appel HTTP.
        var accountIds = Enumerable.Range(1, 201).ToArray();

        var action = async () => await _offerService.GetAccountIdsWithActiveSubscriptionAsync(accountIds, "PENNYLANE");

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*200*");
        // MockBehavior.Strict sans setup SendAsync : tout appel HTTP ferait echouer le test.
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenAccountIdsNull_ReturnsEmptyWithoutCallingHttp()
    {
        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync(null!, "PENNYLANE");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenAccountIdsEmpty_ReturnsEmptyWithoutCallingHttp()
    {
        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync([], "PENNYLANE");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenSuccess_ReturnsIdsAndBuildsQuery()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(new[] { 11, 33 })
        };

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync([11, 22, 33], "OFFER&CODE");

        result.Should().BeEquivalentTo(new[] { 11, 33 });
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        var url = capturedRequest.RequestUri!.ToString();
        url.Should().Contain("/api/subscription/active-account-ids");
        // offerCode echappe, un parametre accountId repete par id.
        url.Should().Contain("offerCode=OFFER%26CODE");
        url.Should().Contain("accountId=11&accountId=22&accountId=33");
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenResponseUnsuccessful_ReturnsNull()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync([11], "PENNYLANE");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenBodyIsJsonNull_ReturnsEmpty()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create<int[]?>(null)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync([11], "PENNYLANE");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountIdsWithActiveSubscriptionAsync_WhenExactlyAtCap_CallsHttp()
    {
        // 200 ids : pile le plafond, doit passer.
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(Array.Empty<int>())
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetAccountIdsWithActiveSubscriptionAsync(
            Enumerable.Range(1, 200).ToArray(), "PENNYLANE");

        result.Should().BeEmpty();
    }

    #endregion

    #region GetOfferByIdAsync Tests

    [Fact]
    public async Task GetOfferByIdAsync_WhenSuccess_ShouldReturnOfferDetails()
    {
        var offerDetails = new OfferDetails
        {
            OfferId = 42,
            Plans = new List<OfferPlan>
            {
                new OfferPlan { PlanId = 1, PlanCode = "COLLABORATIVE" },
                new OfferPlan { PlanId = 2, PlanCode = "ESSENTIAL" }
            }
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(offerDetails)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null && req.RequestUri.ToString().Contains("/api/offers/42")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetOfferByIdAsync(42);

        result.Should().NotBeNull();
        result!.OfferId.Should().Be(42);
        result.Plans.Should().HaveCount(2);
        result.Plans![0].PlanCode.Should().Be("COLLABORATIVE");
    }

    [Fact]
    public async Task GetOfferByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetOfferByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOfferByIdAsync_WhenServerError_ShouldReturnNull()
    {
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var result = await _offerService.GetOfferByIdAsync(42);

        result.Should().BeNull();
    }

    #endregion
}
