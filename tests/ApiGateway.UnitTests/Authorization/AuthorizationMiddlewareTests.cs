using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Identity;
using ApiGateway.Middlewares;
using ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationMiddlewareTests
{
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<IAccountService> _mockAccountService;
    private readonly Mock<IIdentityService> _mockIdentityService;

    public AuthorizationMiddlewareTests()
    {
        _mockContactService = new Mock<IContactService>(MockBehavior.Strict);
        _mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
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
        var logger = Mock.Of<ILogger<Program>>();

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)

            .BuildServiceProvider();

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        var result = await action.Should().ThrowAsync<GatewayException>();
        result.Which.Code.Should().BeEquivalentTo(Errors.NotValidCollaboratorCode);
        result.Which.Message.Should().BeEquivalentTo(string.Format(Errors.NotValidCollaboratorMessage, contactEmail));
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
        var logger = Mock.Of<ILogger<Program>>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        var result = await action.Should().ThrowAsync<GatewayException>();
        result.Which.Code.Should().BeEquivalentTo(Errors.NotValidCollaboratorCode);
        result.Which.Message.Should().BeEquivalentTo(string.Format(Errors.NotValidCollaboratorMessage, contactEmail));
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

        var mockedPermissions = new List<string>() { "COADMI001" };

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(mockedPermissions)
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
        _mockAccountService.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(true).Verifiable();

        bool expectedAccountIdOnePassed = false;
        var mockedAutorisations = new List<string>() { "COADMI001" };
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
                if (accountId == 1)
                {
                    expectedAccountIdOnePassed = true;
                }
            })
            .ReturnsAsync(mockedAutorisations)
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
                if (accountId == 1)
                {
                    expectedAccountIdOnePassed = true;
                }
            })
            .ReturnsAsync(mockedAutorisations)
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
        var logger = Mock.Of<ILogger<Program>>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
        .Callback<string>(email => email.Equals(contactEmail))
        .Returns(Task.FromResult<string>(null!)!)
        .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        var result = await action.Should().ThrowAsync<GatewayException>();
        result.Which.Code.Should().BeEquivalentTo(Errors.NullArgumentCode);
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
        var logger = Mock.Of<ILogger<Program>>();
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims, new());
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(() => null!)
            .Verifiable();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        var result = await action.Should().ThrowAsync<GatewayException>();
        // Assert
        result.Which.Code.Should().Be(Errors.NullArgumentCode);
        result.Which.Message.Should().Be(string.Format(Errors.NullArgumentMessage, "contactId"));
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
        var logger = Mock.Of<ILogger<Program>>();
        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        var cacheService = new Mock<ICacheService>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();

        var mockedEmptyPermissions = new List<string>();

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(mockedEmptyPermissions)
            .Verifiable();

        //_mockIdentityService.Setup(x => x.IsCollaborator(httpContext)).Returns(true);
        _mockIdentityService.Setup(x => x.ValidateCollaborator(httpContext)).Returns(true);

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        var result = await action.Should().ThrowAsync<GatewayException>();

        result.Which.Code.Should().BeEquivalentTo(Errors.PermissionRequiredCode);
        result.Which.Message.Should().BeEquivalentTo(Errors.PermissionRequiredMessage);
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

        _mockAccountService.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(true).Verifiable();

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
        var logger = Mock.Of<ILogger<Program>>();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
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
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);
        var result = await action.Should().ThrowAsync<GatewayException>();

        result.Which.Code.Should().Be(Errors.RoleRequiredCode);
        result.Which.Message.Should().Be(string.Format(Errors.RoleRequiredMessage, 90, 1));

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

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims,new());

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

        var logger = Mock.Of<ILogger<Program>>();

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);
        var result = await action.Should().ThrowAsync<GatewayException>();

        result.Which.Code.Should().Be(Errors.NotValidCustomerCode);
        result.Which.Message.Should().Be(string.Format(Errors.NotValidCustomerMessage, contactEmail));
        _mockContactService.VerifyAll();
        mockIdentityService.Verify(x => x.ValidateCustomerAsync(contactEmail), Times.Once);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenRouteIsAnonymous_ShouldBypassAllValidations()
    {
        // Arrange
        var path = "/gtw/public/api/health";
        var method = "GET";
        var contactEmail = string.Empty;
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims, new(), isAnonymous: true);
        var cacheService = new Mock<ICacheService>();

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(_mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        // Verify that no services were called (bypassed all validations)
        _mockContactService.Verify(x => x.GetContactIdAsync(It.IsAny<string>()), Times.Never);
        _mockAuthorizationService.Verify(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        _mockAuthorizationService.Verify(x => x.GetAllContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        _mockAccountService.Verify(x => x.GetContactRolesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenAdministratorValidationFails_ShouldReturnForbidden()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "admin@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var mockIdentityService = new Mock<IIdentityService>();
        mockIdentityService.Setup(x => x.ValidateAdministrator(It.IsAny<HttpContext>())).Returns(false);

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
             .Callback<string>(email => email.Equals(contactEmail))
             .ReturnsAsync("90")
             .Verifiable();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Administrator") }, "TestAuthType"));
        var cacheService = new Mock<ICacheService>();
        var logger = Mock.Of<ILogger<Program>>();

        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .AddSingleton(_mockAccountService.Object)
            .AddSingleton(mockIdentityService.Object)
            .AddSingleton(cacheService.Object)
            .AddSingleton(logger)
            .BuildServiceProvider();

        // Act
        var action = async () => await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        var result = await action.Should().ThrowAsync<GatewayException>();
        result.Which.Code.Should().BeEquivalentTo(Errors.NotValidAdministratorCode);
        result.Which.Message.Should().BeEquivalentTo(string.Format(Errors.NotValidAdministratorMessage, contactEmail));
    }

    [Fact]
    public async Task AuthorizationFilter_WhenAdministratorValidationSucceeds_ShouldAllowAccess()
    {
        // Arrange
        var path = "/gtw/authorization/api/accounts/1";
        var method = "GET";
        var contactEmail = "admin@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var mockIdentityService = new Mock<IIdentityService>();
        mockIdentityService.Setup(x => x.ValidateAdministrator(It.IsAny<HttpContext>())).Returns(true);

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
             .ReturnsAsync("90")
             .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        _mockAccountService.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(true).Verifiable();

        var httpContext = Dummies.DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Administrator") }, "TestAuthType"));
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
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        mockIdentityService.Verify(x => x.ValidateAdministrator(It.IsAny<HttpContext>()), Times.Once);
    }
}
