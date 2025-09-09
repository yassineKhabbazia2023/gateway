using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Models;
using ApiGateway.Offer;
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
}

