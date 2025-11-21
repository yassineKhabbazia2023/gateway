using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.Account;
using ApiGateway.Authorization;
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

    #endregion
}
