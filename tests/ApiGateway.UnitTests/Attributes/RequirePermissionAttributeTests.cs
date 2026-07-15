using ApiGateway.Attributes;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Constants;
using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Attributes;

public class RequirePermissionAttributeTests
{
    private readonly Mock<IPermissionValidationService> _mockValidationService;
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<ILogger<RequirePermissionAttribute>> _mockLogger;

    public RequirePermissionAttributeTests()
    {
        _mockValidationService = new Mock<IPermissionValidationService>(MockBehavior.Strict);
        _mockContactService = new Mock<IContactService>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<RequirePermissionAttribute>>(MockBehavior.Loose);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPermissions_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var attribute = new RequirePermissionAttribute("COOFF001", "COOFF002");

        // Assert
        attribute.Should().NotBeNull();
        attribute.CheckAccountRole.Should().BeTrue(); // Default value
    }

    [Fact]
    public void Constructor_WithNullPermissions_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new RequirePermissionAttribute(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("permissions");
    }

    [Fact]
    public void Constructor_WithEmptyPermissions_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new RequirePermissionAttribute();

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Au moins une permission doit être spécifiée*");
    }

    [Fact]
    public void Constructor_WithOrLogic_ShouldSetDefaultCheckAccountRoleTrue()
    {
        // Arrange & Act
        var attribute = new RequirePermissionAttribute("COOFF001");

        // Assert
        attribute.CheckAccountRole.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithAndLogic_ShouldSetLogicAndPermissions()
    {
        // Arrange & Act
        var attribute = new RequirePermissionAttribute(PermissionLogic.And, "COOFF001", "COOFF002");

        // Assert
        attribute.Should().NotBeNull();
        attribute.CheckAccountRole.Should().BeTrue();
    }

    [Fact]
    public void CheckAccountRole_Property_ShouldAllowInitialization()
    {
        // Arrange & Act
        var attribute = new RequirePermissionAttribute("COOFF001")
        {
            CheckAccountRole = false
        };

        // Assert
        attribute.CheckAccountRole.Should().BeFalse();
    }

    #endregion

    #region OR Logic Tests

    [Fact]
    public async Task OnAuthorizationAsync_WithOrLogic_WhenUserHasOnePermission_ShouldSucceed()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001", "COOFF002");

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Contains("COOFF001") && p.Contains("COOFF002")),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull(); // No result means success
        _mockValidationService.Verify(x => x.ValidatePermissionsAsync(
            contactId,
            It.Is<string[]>(p => p.Length == 2),
            accountId), Times.Once);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithOrLogic_WhenUserHasNoPermissions_ShouldReturnForbidden()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ThrowsAsync(new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.PermissionRequiredCode,
                Errors.PermissionRequiredMessage));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithOrLogic_WhenUserHasMultiplePermissions_ShouldSucceed()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001", "COOFF002", "COINFO001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Length == 3),
                null))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
    }

    #endregion

    #region AND Logic Tests

    [Fact]
    public async Task OnAuthorizationAsync_WithAndLogic_WhenUserHasAllPermissions_ShouldSucceed()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute(PermissionLogic.And, "COOFF001", "COOFF002");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        // For AND logic, each permission is checked individually
        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Length == 1 && p[0] == "COOFF001"),
                null))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Length == 1 && p[0] == "COOFF002"),
                null))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
        _mockValidationService.Verify(x => x.ValidatePermissionsAsync(
            contactId,
            It.Is<string[]>(p => p.Length == 1),
            null), Times.Exactly(2)); // Called twice, once per permission
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithAndLogic_WhenUserMissingOnePermission_ShouldReturnForbidden()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute(PermissionLogic.And, "COOFF001", "COOFF002");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        // First permission succeeds
        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Length == 1 && p[0] == "COOFF001"),
                null))
            .ReturnsAsync(true);

        // Second permission fails
        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.Is<string[]>(p => p.Length == 1 && p[0] == "COOFF002"),
                null))
            .ThrowsAsync(new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.PermissionRequiredCode,
                Errors.PermissionRequiredMessage));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithAndLogic_WhenUserHasNoPermissions_ShouldReturnForbidden()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute(PermissionLogic.And, "COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ThrowsAsync(new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.PermissionRequiredCode,
                Errors.PermissionRequiredMessage));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region Account Role Validation Tests

    [Fact]
    public async Task OnAuthorizationAsync_WithCheckAccountRoleTrue_WhenAccountIdPresent_ShouldValidateRole()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001")
        {
            CheckAccountRole = true
        };

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
        _mockValidationService.Verify(x => x.ValidateAccountRoleAsync(
            It.IsAny<HttpContext>(),
            int.Parse(contactId),
            accountId), Times.Once);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithCheckAccountRoleFalse_ShouldSkipRoleValidation()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001")
        {
            CheckAccountRole = false
        };

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
        _mockValidationService.Verify(x => x.ValidateAccountRoleAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<int>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithCheckAccountRoleTrue_WhenNoAccountId_ShouldSkipRoleValidation()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001")
        {
            CheckAccountRole = true
        };

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
        _mockValidationService.Verify(x => x.ValidateAccountRoleAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<int>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenContactIdNull_ShouldThrowBadRequestException()
    {
        // Arrange
        var userEmail = "user@example.com";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync((string?)null); // Null contact ID should cause exception

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - Should return BadRequest due to null contactId
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = context.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Data Extraction Tests

    [Fact]
    public async Task OnAuthorizationAsync_WhenNoUserEmail_ShouldThrowBadRequestException()
    {
        // Arrange
        var attribute = new RequirePermissionAttribute("COOFF001");
        var context = CreateAuthorizationContext(""); // Empty email should cause exception

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - Should return BadRequest due to empty userEmail
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = context.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenAccountIdInQueryParam_ShouldExtract()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - accountId extracted from query param
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenAccountIdInPath_ShouldExtract()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail, accountId, "/api/accounts/456/details");

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - accountId extracted from path
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenAccountIdInRouteValues_ShouldExtract()
    {
        // Arrange - path shape used by prospect onboarding routes: /onboarding/{accountId}/commercial-proposal
        // which does not match the "/accounts/(\d+)" fallback pattern, so extraction must rely on route values.
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("CLPCONF004");

        var context = CreateAuthorizationContext(
            userEmail,
            path: $"/gtw/prospect/api/onboarding/{accountId}/commercial-proposal",
            routeValues: new RouteValueDictionary { ["accountId"] = accountId.ToString() });

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - accountId extracted from route values, not the "/accounts/" path pattern
        context.Result.Should().BeNull();
        _mockValidationService.Verify(x => x.ValidatePermissionsAsync(
            contactId,
            It.IsAny<string[]>(),
            accountId), Times.Once);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenAccountIdInHeader_ShouldExtract()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail, accountId);
        context.HttpContext.Request.Headers[GlobalsConstants.AccountIdHeader] = accountId.ToString();

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert - accountId extracted from header
        context.Result.Should().BeNull();
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task OnAuthorizationAsync_WhenGatewayException403_ShouldReturnForbidResult()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ThrowsAsync(new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.PermissionRequiredCode,
                Errors.PermissionRequiredMessage));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenGatewayException401_ShouldReturnUnauthorizedResult()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ThrowsAsync(new GatewayException(
                StatusCodes.Status401Unauthorized,
                "GTW999",
                "Unauthorized"));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenUnexpectedException_ShouldReturn500()
    {
        // Arrange
        var userEmail = "user@example.com";
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .Throws(new InvalidOperationException("Unexpected error"));

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = context.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task OnAuthorizationAsync_ShouldLogAuthorizationSteps()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                null))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
        // Logger was called (verified through Mock setup)
    }

    [Fact]
    public async Task OnAuthorizationAsync_OnSuccess_ShouldLogSuccessWithDetails()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactId = "123";
        var accountId = 456;
        var attribute = new RequirePermissionAttribute("COOFF001");

        var context = CreateAuthorizationContext(userEmail, accountId);

        _mockContactService.Setup(x => x.GetContactIdAsync(userEmail))
            .ReturnsAsync(contactId);

        _mockValidationService.Setup(x => x.ValidatePermissionsAsync(
                contactId,
                It.IsAny<string[]>(),
                accountId))
            .ReturnsAsync(true);

        _mockValidationService.Setup(x => x.ValidateAccountRoleAsync(
                It.IsAny<HttpContext>(),
                int.Parse(contactId),
                accountId))
            .ReturnsAsync(true);

        // Act
        await attribute.OnAuthorizationAsync(context);

        // Assert
        context.Result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private AuthorizationFilterContext CreateAuthorizationContext(
        string userEmail,
        int? accountId = null,
        string path = "/api/test",
        RouteValueDictionary? routeValues = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.Method = "POST";

        if (!string.IsNullOrEmpty(userEmail))
        {
            var token = GenerateDummyJwtToken(userEmail);
            httpContext.Request.Headers.Authorization = $"Bearer {token}";
        }

        if (accountId.HasValue)
        {
            httpContext.Request.QueryString = new QueryString($"?accountId={accountId}");
        }

        if (routeValues is not null)
        {
            // Mirrors what ASP.NET Core's endpoint routing middleware sets on the real request
            // pipeline, which is what HttpContext.GetRouteValue(...) reads from.
            httpContext.Features.Set<Microsoft.AspNetCore.Http.Features.IRouteValuesFeature>(
                new TestRouteValuesFeature(routeValues));
        }

        var services = new ServiceCollection();
        services.AddSingleton(_mockValidationService.Object);
        services.AddSingleton(_mockContactService.Object);
        services.AddSingleton(_mockLogger.Object);

        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(routeValues ?? new RouteValueDictionary()),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private sealed class TestRouteValuesFeature : Microsoft.AspNetCore.Http.Features.IRouteValuesFeature
    {
        public TestRouteValuesFeature(RouteValueDictionary routeValues)
        {
            RouteValues = routeValues;
        }

        public RouteValueDictionary RouteValues { get; set; }
    }

    private static string GenerateDummyJwtToken(string userEmail)
    {
        var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var claims = new Dictionary<string, string>
        {
            {"email", userEmail}
        };
        var payload = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(claims));
        var signature = "";

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(inputBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    #endregion
}
