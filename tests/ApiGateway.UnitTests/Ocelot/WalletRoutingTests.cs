using System.Net;
using System.Web;
using ApiGateway.UnitTests.Mocks;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.UnitTests.Permissions;

/// <summary>Exercises Wallet proxying over HTTP through Ocelot and the existing security pipeline.</summary>
public sealed class WalletRoutingTests : IAsyncLifetime
{
    private const string WalletPath = "/gtw/wallet/api/currentuser";
    private readonly WalletGatewayHost _host = new();

    /// <inheritdoc/>
    public Task InitializeAsync() => _host.InitializeAsync();

    /// <inheritdoc/>
    public Task DisposeAsync() => _host.DisposeAsync();

    #region Routing and parameters

    /// <summary>Preserves the Account envelope and resolves identity even without browser identity headers.</summary>
    /// <param name="scheme">The accepted authentication provider.</param>
    [Theory]
    [InlineData("AAD")]
    [InlineData("GIGYA MyPulse v2")]
    public async Task GetWallet_WhenAuthenticated_ShouldForwardIdentityAndUnchangedResponse(string scheme)
    {
        using var request = WalletGatewayHost.CreateRequest(WalletPath, scheme);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(_host.ResponseBody);
        var downstream = _host.Requests.Should().ContainSingle().Subject;
        downstream.Method.Should().Be(HttpMethod.Get);
        downstream.RequestUri!.AbsolutePath.Should().Be("/api/wallet");
        downstream.Headers.GetValues("CurrentUser").Should().Equal("42");
        downstream.Headers.GetValues("ContactEmail").Should().Equal("wallet@example.com");
        downstream.Headers.GetValues("ContactType").Should().Equal("Collaborateur");
        downstream.Headers.GetValues("X-Correlation-Id").Should().Equal("wallet-routing-test");
    }

    /// <summary>Forwards every Wallet filter, repeated values, encoded search and explicit false flags without changing semantics.</summary>
    /// <param name="statistics">The explicit statistics inclusion flag.</param>
    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public async Task GetWallet_WhenFiltersAndForgedIdentityAreProvided_ShouldPreserveFiltersAndReplaceIdentity(string statistics)
    {
        var query = "PageNumber=2&PageSize=120&Search=R%26D%20%2B%20Paris"
            + "&DeploymentStatus=1&DeploymentStatus=2&MissionType=Accounting&MissionType=Payroll"
            + "&LastActivityDateFrom=2026-01-01T00%3A00%3A00Z&LastActivityDateTo=2026-09-01T00%3A00%3A00Z"
            + "&IsFavoriteFilter=false&IsCustomerRelationFilter=true&Sorting.Field=companyName&Sorting.Descending=false"
            + $"&IncludeFavorites=false&IncludeStatistics={statistics}";
        using var request = WalletGatewayHost.CreateRequest(
            $"{WalletPath}?{query}", spoofIdentity: true);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = _host.Requests.Should().ContainSingle().Subject;
        var actual = HttpUtility.ParseQueryString(downstream.RequestUri!.Query);
        var expected = HttpUtility.ParseQueryString(query);
        foreach (var key in expected.AllKeys)
        {
            actual.GetValues(key).Should().Equal(expected.GetValues(key));
        }

        actual.Count.Should().Be(expected.Count);
        downstream.Headers.GetValues("CurrentUser").Should().Equal("42");
        downstream.Headers.GetValues("ContactEmail").Should().Equal("wallet@example.com");
        downstream.Headers.GetValues("ContactType").Should().Equal("Collaborateur");
    }

    /// <summary>Leaves defaults and validation to Account, including invalid and omitted page sizes.</summary>
    /// <param name="pageSize">The optional requested page size.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("90")]
    [InlineData("120")]
    [InlineData("0")]
    [InlineData("121")]
    [InlineData("invalid")]
    public async Task GetWallet_WhenPageSizeIsProvidedOrOmitted_ShouldForwardItUnchanged(string? pageSize)
    {
        using var request = WalletGatewayHost.CreateRequest(WalletPath + (pageSize is null ? "" : $"?PageSize={pageSize}"));

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = _host.Requests.Should().ContainSingle().Subject;
        HttpUtility.ParseQueryString(downstream.RequestUri!.Query)["PageSize"].Should().Be(pageSize);
    }

    /// <summary>Proves the legacy list and independent statistics remain reachable alongside the new route.</summary>
    /// <param name="upstream">The existing frontend route.</param>
    /// <param name="downstreamPath">The path after current-user identity preparation.</param>
    [Theory]
    [InlineData("/gtw/account/api/accounts/currentuser", "/api/accounts")]
    [InlineData("/gtw/account/api/accounts/currentuser?pageNumber=2&pageSize=90&search=Paris", "/api/accounts")]
    [InlineData("/gtw/wallet/api/statistics/currentuser", "/api/statistics")]
    public async Task GetLegacyRoute_WhenWalletRouteExists_ShouldStillReachOriginalEndpoint(string upstream, string downstreamPath)
    {
        using var request = WalletGatewayHost.CreateRequest(upstream);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _host.Requests.Should().ContainSingle().Subject.RequestUri!.AbsolutePath.Should().Be(downstreamPath);
    }

    #endregion

    #region Errors and security

    /// <summary>Preserves Account validation, authorization, not-found and technical errors without manufacturing an empty Wallet.</summary>
    /// <param name="status">The downstream error status.</param>
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task GetWallet_WhenAccountReturnsError_ShouldPreserveStatusAndBody(int status)
    {
        _host.ResponseStatus = status;
        _host.ResponseBody = "{\"errorCode\":\"ACCOUNT_ERROR\",\"errorMessage\":\"Account failure\"}";
        using var request = WalletGatewayHost.CreateRequest(WalletPath);

        using var response = await _host.Client.SendAsync(request);

        ((int)response.StatusCode).Should().Be(status);
        (await response.Content.ReadAsStringAsync()).Should().Be(_host.ResponseBody);
        _host.Requests.Should().ContainSingle();
    }

    /// <summary>Rejects missing or invalid JWTs before any downstream request, even with forged identity headers.</summary>
    /// <param name="token">The optional invalid bearer token.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task GetWallet_WhenUnauthenticated_ShouldRejectRequest(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WalletPath);
        request.Headers.Add("CurrentUser", "999");
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _host.Requests.Should().BeEmpty();
    }

    /// <summary>Retains the existing collaborator membership authorization check.</summary>
    [Fact]
    public async Task GetWallet_WhenCollaboratorIsDenied_ShouldRejectBeforeDownstream()
    {
        _host.IdentityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(false);
        using var request = WalletGatewayHost.CreateRequest(WalletPath);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _host.Requests.Should().BeEmpty();
    }

    /// <summary>Strips forged headers when contact resolution returns no contact.</summary>
    [Fact]
    public async Task GetWallet_WhenContactIsMissing_ShouldNeverForwardForgedIdentityHeaders()
    {
        _host.ContactService.Setup(service => service.GetContactAsync("wallet@example.com"))
            .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);
        _host.ResponseStatus = 400;
        using var request = WalletGatewayHost.CreateRequest(WalletPath, spoofIdentity: true);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var downstream = _host.Requests.Should().ContainSingle().Subject;
        downstream.Headers.Contains("CurrentUser").Should().BeFalse();
        downstream.Headers.Contains("ContactEmail").Should().BeFalse();
        downstream.Headers.Contains("ContactType").Should().BeFalse();
    }

    /// <summary>Does not send an Account request when contact resolution fails unexpectedly.</summary>
    [Fact]
    public async Task GetWallet_WhenContactServiceFails_ShouldNotForwardRequest()
    {
        _host.ContactService.Setup(service => service.GetContactAsync("wallet@example.com"))
            .ThrowsAsync(new HttpRequestException("Contact unavailable"));
        using var request = WalletGatewayHost.CreateRequest(WalletPath, spoofIdentity: true);

        using var response = await _host.Client.SendAsync(request);

        response.IsSuccessStatusCode.Should().BeFalse();
        _host.Requests.Should().BeEmpty();
    }

    /// <summary>Preserves Ocelot's unavailable-downstream behavior.</summary>
    [Fact]
    public async Task GetWallet_WhenAccountIsUnavailable_ShouldReturnBadGateway()
    {
        await _host.StopAccountAsync();
        using var request = WalletGatewayHost.CreateRequest(WalletPath);

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        _host.Requests.Should().BeEmpty();
    }

    /// <summary>Does not expose write methods on the optimized Wallet route.</summary>
    [Fact]
    public async Task GetWallet_WhenMethodIsPost_ShouldNotMatchRoute()
    {
        using var request = WalletGatewayHost.CreateRequest(WalletPath);
        request.Method = HttpMethod.Post;

        using var response = await _host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _host.Requests.Should().BeEmpty();
    }

    #endregion
}
