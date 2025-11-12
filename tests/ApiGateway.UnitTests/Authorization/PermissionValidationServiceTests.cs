using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Constants;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.UnitTests.Authorization;

public class PermissionValidationServiceTests
{
    private readonly PermissionValidationService _sut;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<IAccountService> _mockAccountService;

    public PermissionValidationServiceTests()
    {
        _mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        _mockAccountService = new Mock<IAccountService>(MockBehavior.Strict);
        _sut = new PermissionValidationService(_mockAuthorizationService.Object);
    }

    #region ValidatePermissionsAsync Tests

    [Fact]
    public async Task ValidatePermissionsAsync_WithNoRequiredPermissions_ShouldReturnTrue()
    {
        // Arrange
        var contactId = "123";
        var requiredPermissions = Array.Empty<string>();

        // Act
        var result = await _sut.ValidatePermissionsAsync(contactId, requiredPermissions);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WithNullRequiredPermissions_ShouldReturnTrue()
    {
        // Arrange
        var contactId = "123";

        // Act
        var result = await _sut.ValidatePermissionsAsync(contactId, null!);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenUserHasPermission_ShouldReturnTrue()
    {
        // Arrange
        var contactId = "123";
        var accountId = 456;
        var requiredPermissions = new[] { "COOFF001" };
        var userPermissions = new List<string> { "COOFF001", "COINFO001" };

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), accountId))
            .ReturnsAsync(userPermissions);

        // Act
        var result = await _sut.ValidatePermissionsAsync(contactId, requiredPermissions, accountId);

        // Assert
        result.Should().BeTrue();
        _mockAuthorizationService.Verify(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), accountId), Times.Once);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenUserHasNoPermission_ShouldThrowForbiddenException()
    {
        // Arrange
        var contactId = "123";
        var accountId = 456;
        var requiredPermissions = new[] { "COOFF001" };
        var userPermissions = new List<string> { "COINFO001" }; // Missing COOFF001

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), accountId))
            .ReturnsAsync(userPermissions);

        // Act
        var action = async () => await _sut.ValidatePermissionsAsync(contactId, requiredPermissions, accountId);

        // Assert
        var exception = await action.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        exception.Which.Code.Should().Be(Errors.PermissionRequiredCode);
        exception.Which.Message.Should().Be(Errors.PermissionRequiredMessage);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenContactIdNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var requiredPermissions = new[] { "COOFF001" };

        // Act
        var action = async () => await _sut.ValidatePermissionsAsync(null!, requiredPermissions);

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("contactId");
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenContactIdEmpty_ShouldThrowArgumentNullException()
    {
        // Arrange
        var requiredPermissions = new[] { "COOFF001" };

        // Act
        var action = async () => await _sut.ValidatePermissionsAsync("", requiredPermissions);

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("contactId");
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WithAccountId_ShouldPassToAuthorizationService()
    {
        // Arrange
        var contactId = "123";
        var accountId = 456;
        var requiredPermissions = new[] { "COOFF001" };
        var userPermissions = new List<string> { "COOFF001" };

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), accountId))
            .ReturnsAsync(userPermissions);

        // Act
        await _sut.ValidatePermissionsAsync(contactId, requiredPermissions, accountId);

        // Assert
        _mockAuthorizationService.Verify(
            x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), accountId),
            Times.Once);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_PermissionComparisonCaseInsensitive_ShouldSucceed()
    {
        // Arrange
        var contactId = "123";
        var requiredPermissions = new[] { "cooff001" }; // lowercase
        var userPermissions = new List<string> { "COOFF001" }; // UPPERCASE

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), null))
            .ReturnsAsync(userPermissions);

        // Act
        var result = await _sut.ValidatePermissionsAsync(contactId, requiredPermissions);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenUserHasOneOfMultiplePermissions_ShouldSucceed()
    {
        // Arrange
        var contactId = "123";
        var requiredPermissions = new[] { "COOFF001", "COOFF002", "COINFO001" };
        var userPermissions = new List<string> { "COINFO001" }; // Has only one

        _mockAuthorizationService.Setup(x => x.GetAllContactAuthorizationAsync(int.Parse(contactId), null))
            .ReturnsAsync(userPermissions);

        // Act
        var result = await _sut.ValidatePermissionsAsync(contactId, requiredPermissions);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ValidateAccountRoleAsync Tests

    [Fact]
    public async Task ValidateAccountRoleAsync_WhenNoAccountId_ShouldReturnTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var contactId = 123;

        // Act
        var result = await _sut.ValidateAccountRoleAsync(httpContext, contactId, null);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccountRoleAsync_WhenSkipRoleCheck_ShouldReturnTrue()
    {
        // Arrange
        var contactId = 123;
        var accountId = 456;
        var httpContext = new DefaultHttpContext();

        // Setup SkipRoleCheck to return true (user has permission from NoRoleCheckPermissions)
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(contactId, accountId))
            .ReturnsAsync(new List<string> { "COADMI004" }); // COADMI004 is in NoRoleCheckPermissions

        SetupServices(httpContext);

        // Act
        var result = await _sut.ValidateAccountRoleAsync(httpContext, contactId, accountId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAccountRoleAsync_WhenUserHasRole_ShouldReturnTrue()
    {
        // Arrange
        var contactId = 123;
        var accountId = 456;
        var httpContext = new DefaultHttpContext();

        // Setup SkipRoleCheck to return false
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(contactId, accountId))
            .ReturnsAsync(new List<string>());

        // Setup CheckRoles to return true
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(contactId, GlobalsConstants.CollaboratorAccountId))
            .ReturnsAsync(new List<string>());

        _mockAccountService.Setup(x => x.CheckContactRoleAsync(contactId, accountId, null))
            .ReturnsAsync(true);

        SetupServices(httpContext);

        // Act
        var result = await _sut.ValidateAccountRoleAsync(httpContext, contactId, accountId);

        // Assert
        result.Should().BeTrue();
        _mockAccountService.Verify(x => x.CheckContactRoleAsync(contactId, accountId, null), Times.Once);
    }

    [Fact]
    public async Task ValidateAccountRoleAsync_WhenUserHasNoRole_ShouldThrowForbiddenException()
    {
        // Arrange
        var contactId = 123;
        var accountId = 456;
        var httpContext = new DefaultHttpContext();

        // Setup SkipRoleCheck to return false
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(contactId, accountId))
            .ReturnsAsync(new List<string>());

        // Setup CheckRoles to return false
        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(contactId, GlobalsConstants.CollaboratorAccountId))
            .ReturnsAsync(new List<string>());

        _mockAccountService.Setup(x => x.CheckContactRoleAsync(contactId, accountId, null))
            .ReturnsAsync(false);

        SetupServices(httpContext);

        // Act
        var action = async () => await _sut.ValidateAccountRoleAsync(httpContext, contactId, accountId);

        // Assert
        var exception = await action.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        exception.Which.Code.Should().Be(Errors.RoleRequiredCode);
        exception.Which.Message.Should().Contain(contactId.ToString());
        exception.Which.Message.Should().Contain(accountId.ToString());
    }

    #endregion

    #region Helper Methods

    private void SetupServices(HttpContext httpContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockAuthorizationService.Object);
        services.AddSingleton(_mockAccountService.Object);

        httpContext.RequestServices = services.BuildServiceProvider();
    }

    #endregion
}
