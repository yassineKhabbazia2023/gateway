using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.Account;
using ApiGateway.Models;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Services;
using Moq.Protected;

namespace ApiGateway.UnitTests.Authorization;

public class AccountServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IProspectApiClient> _mockProspectApiClient;
    private readonly HttpClient _httpClient;
    private readonly AccountService _accountService;
    private readonly Fixture _fixture;
    private readonly string _collabType = "Collaborator";

    public AccountServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _mockProspectApiClient = new Mock<IProspectApiClient>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _httpClient.BaseAddress = new Uri("http://local.account/api");
        _accountService = new AccountService(_httpClient, _mockProspectApiClient.Object);
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

    [Fact]
    public async Task GetSummaryAsync_WhenResponseUnsuccessful_ReturnsNull()
    {
        var accountId = 193216;
        var contactId = 123;
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
        var result = await _accountService.GetSummaryAsync(accountId, contactId, _collabType);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_WhenAccountExists_ReturnsSummary()
    {
        var accountId = 193216;
        var contactId = 0;
        var account = new Fixture().Create<Summary>();
        account.Signatory!.ContactId = contactId;

        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(account)
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.GetSummaryAsync(accountId, contactId, _collabType);
        result.Should().BeEquivalentTo(account);
    }

    /// <summary>
    /// Ensures the Account client maps the downstream prospect-only boolean response.
    /// </summary>
    /// <param name="isProspectOnly">The downstream boolean response.</param>
    /// <param name="expectedResult">The expected gateway result.</param>
    [Theory]
    [InlineData(true, ProspectOnlyContactResult.ProspectOnly)]
    [InlineData(false, ProspectOnlyContactResult.NotProspectOnly)]
    public async Task GetProspectOnlyContactResultAsync_WhenAccountReturnsBoolean_MapsResult(
        bool isProspectOnly,
        ProspectOnlyContactResult expectedResult)
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(isProspectOnly)
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri.Should().Be("http://local.account/api/accounts/contacts/42/is-prospect-only");
            })
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.GetProspectOnlyContactResultAsync(42, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
    }

    /// <summary>
    /// Ensures the Account client maps downstream 404 to the NotFound result.
    /// </summary>
    [Fact]
    public async Task GetProspectOnlyContactResultAsync_WhenAccountReturnsNotFound_MapsNotFound()
    {
        // Arrange
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
        var result = await _accountService.GetProspectOnlyContactResultAsync(42, CancellationToken.None);

        // Assert
        result.Should().Be(ProspectOnlyContactResult.NotFound);
    }

    /// <summary>
    /// Ensures unexpected Account downstream status codes are propagated as HTTP failures.
    /// </summary>
    [Fact]
    public async Task GetProspectOnlyContactResultAsync_WhenAccountReturnsUnexpectedStatus_Throws()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadGateway
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        Func<Task> act = () => _accountService.GetProspectOnlyContactResultAsync(42, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that GetSummaryAsync enriches Summary with prospectId when AccountType is PROSPECT.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_WhenAccountTypeIsProspect_EnrichesWithProspectId()
    {
        // Arrange
        const int accountId = 42;
        const int contactId = 100;
        const int prospectId = 123;

        var summary = new Summary
        {
            AccountId = accountId,
            AccountType = "PROSPECT",
            LegalName = "Test Company"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(summary)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        _mockProspectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prospectId);

        // Act
        var result = await _accountService.GetSummaryAsync(accountId, contactId, _collabType);

        // Assert
        result.Should().NotBeNull();
        result!.ProspectId.Should().Be(prospectId);
        _mockProspectApiClient.Verify(
            client => client.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetSummaryAsync does NOT call ProspectApiClient when AccountType is not PROSPECT.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_WhenAccountTypeIsNotProspect_DoesNotCallProspectApi()
    {
        // Arrange
        const int accountId = 42;
        const int contactId = 100;

        var summary = new Summary
        {
            AccountId = accountId,
            AccountType = "CUSTOMER",
            LegalName = "Test Company"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(summary)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _accountService.GetSummaryAsync(accountId, contactId, _collabType);

        // Assert
        result.Should().NotBeNull();
        result!.ProspectId.Should().BeNull();
        _mockProspectApiClient.Verify(
            client => client.GetProspectIdByAccountIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that GetSummaryAsync sets prospectId to null when ProspectApiClient returns null.
    /// </summary>
    [Fact]
    public async Task GetSummaryAsync_WhenProspectNotLinked_SetsProspectIdToNull()
    {
        // Arrange
        const int accountId = 42;
        const int contactId = 100;

        var summary = new Summary
        {
            AccountId = accountId,
            AccountType = "PROSPECT",
            LegalName = "Test Company"
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(summary)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        _mockProspectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Act
        var result = await _accountService.GetSummaryAsync(accountId, contactId, _collabType);

        // Assert
        result.Should().NotBeNull();
        result!.ProspectId.Should().BeNull();
        _mockProspectApiClient.Verify(
            client => client.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
