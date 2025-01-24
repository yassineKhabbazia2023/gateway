using System.Net;
using System.Text.Json;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Models;
using Moq.Protected;

namespace ApiGateway.UnitTests.Authorization;

public class AccountServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly AccountService _accountService;
    private readonly Fixture _fixture;

    public AccountServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _httpClient.BaseAddress = new Uri("http://local.account/api");
        _accountService = new AccountService(_httpClient);
        _fixture = new Fixture();
    }

    [Fact]
    public async Task GetContactRolesAsync_WhenContactHasRoles_ReturnsContactAccountsPaging()
    {
        // Arrange
        var accounts = _fixture.CreateMany<Models.Account>();
        var expected = new Paging<Models.Account>
        {
            Items = accounts.ToList()
        };
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(expected)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
             .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
             {
                 request.RequestUri.Should().Be("http://local.account/api/roles?contactId=1");
             })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.GetContactRolesAsync(1);

        // Assert
        Assert.Equivalent(expected, result);
    }

    [Fact]
    public async Task GetContactRolesAsync_WhenResponseUnsuccessful_ReturnsEmptyAccountsPaging()
    {
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
        var result = await _accountService.GetContactRolesAsync(1);

        // Assert
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CheckContactRoleAsync_WhenContactHasRoles_ReturnsTrue()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(true)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
             .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
             {
                 request.RequestUri.Should().Be("http://local.account/api/roles/check-contact-role-on-account?contactId=1&accountId=1");
             })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.CheckContactRoleAsync(1,1,null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CheckContactRoleAsync_WhenContactHasntRoles_ReturnsFalse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(false)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
             .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
             {
                 request.RequestUri.Should().Be("http://local.account/api/roles/check-contact-role-on-account?contactId=1&accountId=2");
             })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.CheckContactRoleAsync(1, 2, null);

        // Assert
        Assert.False(result);
    }


    [Fact]
    public async Task GetAccountAsync_WhenContactHasRoles_ReturnsAccount()
    {
        // Arrange
        var accounts = _fixture.CreateMany<Models.Account>();
        var expected = accounts.First();
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(expected)),
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
             .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
             {
                 request.RequestUri.Should().Be("http://local.account/api/accounts/1");
             })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.GetAccountAsync(1);

        // Assert
        Assert.Equivalent(expected, result);
    }

    [Fact]
    public async Task GetAccountAsync_WhenResponseUnsuccessful_ReturnsNull()
    {
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
        var action = async () => await _accountService.GetAccountAsync(1);
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}

