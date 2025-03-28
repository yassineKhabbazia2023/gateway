using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using ApiGateway.UnitTests.Mocks;
using ApiGateway.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Net;
using ApiGateway.Account;
using ApiGateway.DelegatingHandlers;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class RoleHandlerTests
{
  private readonly Mock<ILogger<RoleHandler>> _loggerMock;
  private readonly IHttpContextAccessor _httpContextAccessorMock;
  private readonly Mock<IAuthorizationSevice> _authorizationServiceMock;
  private readonly Mock<IAccountService> _accountServiceMock;
  private readonly TestableRoleHandler _roleHandler;

  public RoleHandlerTests()
  {
    // Set up default response mock
    var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent("Test response")
    };
    var mockHandler = new MockHttpMessageHandler(mockResponse);

    // Initialize mocks
    _loggerMock = new Mock<ILogger<RoleHandler>>();
    _httpContextAccessorMock = new HttpContextAccessor();

    // Setup HttpContext with necessary details
    var contextPath = "/gtw/account/api/roles";
    var requestMethod = "GET";
    var contactEmail = "user@rydge.fr";
    var requiredClaims = new Dictionary<string, string>();

    _httpContextAccessorMock.HttpContext = Dummies.DummyHttpContext(contextPath, requestMethod, contactEmail, requiredClaims);
    _httpContextAccessorMock.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Collaborator") }, "TestAuthType"));

    // Mock Authorization and Account services
    _authorizationServiceMock = new Mock<IAuthorizationSevice>(MockBehavior.Strict);
    _accountServiceMock = new Mock<IAccountService>();

    // Initialize the RoleHandler middleware with mocked dependencies
    _roleHandler = new TestableRoleHandler(
        _loggerMock.Object,
        _httpContextAccessorMock,
        mockHandler
    );

    // Configure service provider with mocked services
    var serviceProvider = new ServiceCollection()
        .AddSingleton(_authorizationServiceMock.Object)
        .AddSingleton(_accountServiceMock.Object)
        .BuildServiceProvider();

    // Inject services into HttpContext
    var mockHttpContext = new DefaultHttpContext { RequestServices = serviceProvider };
    _httpContextAccessorMock.HttpContext = mockHttpContext;
  }

  #region Test Cases

  // Test: Missing 'CurrentUser' header
  [Fact]
  public async Task ShouldReturnForbidden_WhenCurrentUserHeaderIsMissing()
  {
    var request = new HttpRequestMessage();
    request.Headers.Remove("CurrentUser"); // Remove 'CurrentUser' header to simulate missing header

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().BeEquivalentTo("{\"ErrorMessage\":\"Le contact avec l'identifiant CurrentUser est introuvable\",\"ErrorCode\":\"GTW003\"}");
  }

  // Test: Invalid 'CurrentUser' contactId
  [Fact]
  public async Task ShouldReturnForbidden_WhenCurrentUserHasInvalidContactId()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "invalidId"); // Simulate invalid contactId in 'CurrentUser' header

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().BeEquivalentTo("{\"ErrorMessage\":\"Le contact avec l'identifiant CurrentUser est introuvable\",\"ErrorCode\":\"GTW003\"}");
  }

  // Test: User accessing their own information (skip role check)
  [Fact]
  public async Task ShouldSkipRoleCheck_WhenUserAccessesOwnInfo()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "1");

    var queryString = "?contactId=1";  // User is accessing their own information
    request.RequestUri = new Uri("http://test.com" + queryString);

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.OK); // Request should pass without role check
  }

  // Test: Super Admin user (skip role check)
  [Fact]
  public async Task ShouldSkipRoleCheck_WhenUserIsSuperAdmin()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "1");
    request.RequestUri = new Uri("http://test.com");

    // Mock behavior for super admin role
    _authorizationServiceMock.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
        .ReturnsAsync(new List<string> { "COADMI004" }) // Super admin role code
        .Verifiable();

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.OK); // Super admin should pass without role check
  }

  // Test: User has no role on account
  [Fact]
  public async Task ShouldReturnForbidden_WhenUserHasNoRoleOnAccount()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "2");
    var queryString = "?accountId=1";  // accountId present, but user has no role on this account
    request.RequestUri = new Uri("http://test.com" + queryString);

    // Mocking authorization and account service behavior
    _authorizationServiceMock.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
        .ReturnsAsync(new List<string>())
        .Verifiable();

    _accountServiceMock.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
        .ReturnsAsync(false)
        .Verifiable();

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().BeEquivalentTo("{\"ErrorMessage\":\"Le contact avec l'identifiant 2 n'a pas de rôle dans l'entité 1\",\"ErrorCode\":\"GTW007\"}");
  }

  // Test: User does not have a common account role when accountId is not provided
  [Fact]
  public async Task ShouldReturnForbidden_WhenUserHasNoCommonAccountRole()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "2");
    var queryString = "?contactId=3";  // User is requesting info for another contact
    request.RequestUri = new Uri("http://test.com" + queryString);

    // Mocking authorization and account service behavior
    _authorizationServiceMock.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
        .ReturnsAsync(new List<string>())
        .Verifiable();

    _accountServiceMock.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
        .ReturnsAsync(false)
        .Verifiable();

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().BeEquivalentTo("{\"ErrorMessage\":\"Les Contacts: [2] , [3] n'ont pas de rôle commun au niveau des entités.\",\"ErrorCode\":\"GTW015\"}");
  }

  // Test: Request passes through to the next middleware
  [Fact]
  public async Task ShouldContinueToNextMiddleware_WhenAllChecksPass()
  {
    var request = new HttpRequestMessage();
    request.Headers.Add("CurrentUser", "1");
    var queryString = "?contactId=1"; // User accessing their own contact info
    request.RequestUri = new Uri("http://test.com" + queryString);

    var response = await _roleHandler.TestSendAsync(request, CancellationToken.None);

    response.StatusCode.Should().Be(HttpStatusCode.OK); // Request should pass to the next middleware
  }

  #endregion
}
