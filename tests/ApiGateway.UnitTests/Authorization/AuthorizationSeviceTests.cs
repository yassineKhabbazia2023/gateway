using System.Net;
using System.Text.Json;
using ApiGateway.Authorization;
using Moq.Protected;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationSeviceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly AuthorizationSevice _authorizationSevice;

    public AuthorizationSeviceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _httpClient.BaseAddress = new Uri("http://local.authorization/api");
        _authorizationSevice = new AuthorizationSevice(_httpClient);
    }

    [Fact]
    public async Task GetContactAuthorizationAsync_WhenContactHasAuthorization_ReturnsContactAuthorizationList()
    {
        // Arrange
        var expectedList = new List<string> { "CLADMI001", "COADMI001" };
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(expectedList)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
             .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
             {
                 request.RequestUri.Should().Be("http://local.authorization/api/authorization?contactId=1&accountId=1");
             })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _authorizationSevice.GetContactAuthorizationAsync(1, 1);

        // Assert
        Assert.Equal(expectedList, result);
    }

    [Fact]
    public async Task GetContactAuthorizationAsync_WhenAccountIsNull_ReturnsContactAuthorizationList()
    {
        // Arrange
        var expectedList = new List<string> { "CLADMI001", "COADMI001" };
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(expectedList)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
            {
                request.RequestUri.Should().Be("http://local.authorization/api/authorization?contactId=1");
            })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _authorizationSevice.GetContactAuthorizationAsync(1, null);

        // Assert
        Assert.Equal(expectedList, result);
    }

    [Fact]
    public async Task GetContactAuthorizationAsync_WhenContactDoesntHasAuthorization_ShouldReturnsEmptyList()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(string.Empty),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _authorizationSevice.GetContactAuthorizationAsync(1, 1);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContactAuthorizationAsync_WhenResponseIsUnsuccessful_ShouldReturnsEmptyList()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _authorizationSevice.GetContactAuthorizationAsync(1, 1);

        // Assert
        Assert.Empty(result);
    }
}

