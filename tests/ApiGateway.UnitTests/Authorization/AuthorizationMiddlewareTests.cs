using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.Identity;
using ApiGateway.Middlewares;
using ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Ocelot.Configuration;
using Ocelot.Values;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationMiddlewareTests
{
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<IAuthorizationSevice> _mockAuthorizationService;
    private readonly Mock<IAccountService> _mockAccountService;
    private readonly Mock<IIdentityService> _mockIdentityService;

    public AuthorizationMiddlewareTests()
    {
        _mockContactService = new Mock<IContactService>(MockBehavior.Strict);
        _mockAuthorizationService = new Mock<IAuthorizationSevice>(MockBehavior.Strict);
        _mockAccountService = new Mock<IAccountService>();
        _mockIdentityService = new Mock<IIdentityService>();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenCollaboratorValidationFails_ShouldReturnUnauthorized()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var mockIdentityService = new Mock<IIdentityService>();
        mockIdentityService.Setup(x => x.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(false);

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
             .Callback<string>(email => email.Equals(contactEmail))
             .ReturnsAsync("90")
             .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Collaborator") }, "TestAuthType"));
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
    }

    [Fact]
    public async Task AuthorizationFilter_WhenCustomerValidationFails_ShouldReturnUnauthorized()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var mockIdentityService = new Mock<IIdentityService>();
        mockIdentityService.Setup(x => x.ValidateCustomerAsync(It.IsAny<string>())).ReturnsAsync(false);

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
             .Callback<string>(email => email.Equals(contactEmail))
             .ReturnsAsync("90")
             .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Collaborator") }, "TestAuthType"));
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
    }

    [Fact]
    public async Task AuthorizationFilter_WhenHasRequiredPermissions_ShouldAllowsAccess()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_ShouldRetrievePermissions_WithAccountIDHeader()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var headers = new Dictionary<string, string>
        {
            {"Account-Id", "1"}
        };
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var toReturnRole = new Paging<Models.Account>
        {
            Items = { new Models.Account { AccountId = 1 } }
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims, headers);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();
        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();
        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(toReturnRole).Verifiable();

        bool expectedAccountIdOnePassed = false;
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
                if (accountId == 1)
                {
                    expectedAccountIdOnePassed = true;
                }
            })
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        expectedAccountIdOnePassed.Should().Be(true);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenRequiredClaimIsEmpty_ShouldAllowsAccess()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
                    .ReturnsAsync("90")
                    .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenUserEmailIsEmpty_ShouldReturnForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = string.Empty;
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
        .Callback<string>(email => email.Equals(contactEmail))
        .Returns(Task.FromResult<string>(null!)!)
        .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenContactIdIsEmpty_ShoulsReturnForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(() => null!)
            .Verifiable();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenPermissionsAreEmpty_ForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>())
            .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    

    [Fact]
    public async Task AuthorizationFilter_WhenHasRelatedAccounts_ShouldAllowAccess()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        var toReturn = new Paging<Models.Account>
        {
            Items = { new Models.Account { AccountId = 1 } }
        };

        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenGivenAccountNumber_ShouldAllowAccess()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/0000000001/ged-services";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        var toReturn = new Paging<Models.Account>
        {
            Items = { new Models.Account { AccountId = 12 } }
        };

        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenNoRelatedAccounts_ShouldReturnForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        var toReturn = new Paging<Models.Account>();

        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
    }
    [Fact]
    public async Task AuthorizationFilter_WhenNoRelatedAccounts_ShouldAllowRequest_GivenNoCheckPermissions()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COOFF002" })
            .Verifiable();

        var toReturn = new Paging<Models.Account>();

        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenNoAccountId_ShouldAllowAccess()
    {
        // Arrange
        var path = "/gtw/authorization/api/dummy";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenNoEndpointWithException_ShouldAllowAccess()
    {
        // Arrange
        var path = "/api/authorizations/configuration/account";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenCustomerValidationFails_ShouldReturnForbidden()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();
        var cacheService = new Mock<ICacheService>();

        var mockIdentityService = new Mock<IIdentityService>();
        mockIdentityService
            .Setup(x => x.ValidateCustomerAsync(contactEmail))
            .ReturnsAsync(false);

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>())
            .Verifiable();

        var relatedAccounts = new Paging<Models.Account>
        {
            Items = { new Models.Account { AccountId = 1 } }
        };
        _mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>()))
            .ReturnsAsync(relatedAccounts)
            .Verifiable();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);

        // Add the "Customer" role to the ClaimsPrincipal
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new List<Claim>
                {
                new Claim(ClaimTypes.Email, contactEmail),
                new Claim(ClaimTypes.Role, "Customer") // Add the "Customer" role
                },
                "TestAuthType"
            )
        );

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
        _mockContactService.VerifyAll();
        mockIdentityService.Verify(x => x.ValidateCustomerAsync(contactEmail), Times.Once);
    }



}
