using ApiGateway.Account;
using ApiGateway.ConnectExperience.Controller;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Constants;
using ApiGateway.Contact;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Contact;

public class ConnectControllerTests
{
    private readonly Mock<IConnectServices> _experienceServices;
    private readonly Mock<IAccountService> _accountService;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<ApiGateway.Authorization.IAuthorizationService> _authorizationService;
    private readonly int _contactId;
    private readonly int _accountId;
    private readonly ConnectController _sut;
    private readonly string _collabType;

    private class TestClaimsPrincipal : ClaimsPrincipal
    {
        public override Claim? FindFirst(string type)
        {
            // Keep behavior consistent with the previous tests.
            return new Claim(ClaimValueTypes.Email, "bob@truc.io");
        }
    }

    private class TestUserContext : IUserContext
    {
        public ClaimsPrincipal User => new TestClaimsPrincipal();
    }

    public ConnectControllerTests()
    {
        _experienceServices = new Mock<IConnectServices>();
        _accountService = new Mock<IAccountService>();
        _contactService = new Mock<IContactService>();
        _authorizationService = new Mock<ApiGateway.Authorization.IAuthorizationService>();

        _contactId = 123456;
        _accountId = 316354;
        _collabType = "Collaborator";

        _contactService
            .Setup(s => s.GetContactAsync("bob@truc.io"))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = _contactId });
        _sut = new ConnectController(new TestUserContext(), _experienceServices.Object, _accountService.Object, _contactService.Object);
    }

    private void SetupHttpContextForController()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authorizationService.Object);
        services.AddSingleton(_accountService.Object);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetSummary_ShouldReturnOk_WhenContactHasRoleOnAccount()
    {
        // Arrange
        SetupHttpContextForController();

        // Drive AuthorizationHelper.SkipRoleCheck -> false
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, _accountId))
            .ReturnsAsync(new List<string>()); // no NoRoleCheckPermissions

        // Drive AuthorizationHelper.CheckRoles -> no NoAccountCheckPermissions bypass, then role == true
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, It.IsAny<int?>()))
            .ReturnsAsync(new List<string>()); // no NoAccountCheckPermissions

        _accountService
            .Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null))
            .ReturnsAsync(true);

        _experienceServices
            .Setup(s => s.GetSummaryAsync(_accountId, _contactId, _collabType))
            .ReturnsAsync(new ApiGateway.Models.Summary());

        // Act
        var result = await _sut.GetSummary(_accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }


    [Fact]
    public async Task GetSummary_ShouldReturnOk_WhenSkipRoleCheckIsTrue()
    {
        // Arrange
        SetupHttpContextForController();

        // Make SkipRoleCheck return true by including a permission from NoRoleCheckPermissions ("COADMI004")
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, _accountId))
            .ReturnsAsync(new List<string> { "COADMI004" });

        // For CheckRoles, set the collaborator-account permission lookup to "no bypass"
        // so that the outcome depends solely on SkipRoleCheck being true.
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, GlobalsConstants.CollaboratorAccountId))
            .ReturnsAsync(new List<string>());

        // Even if account role check is false, SkipRoleCheck should bypass it.
        _accountService
            .Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null))
            .ReturnsAsync(false);

        _experienceServices
            .Setup(s => s.GetSummaryAsync(_accountId, _contactId, _collabType))
            .ReturnsAsync(new ApiGateway.Models.Summary());

        // Act
        var result = await _sut.GetSummary(_accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }



    [Fact]
    public async Task GetSummary_ShouldReturnOk_WhenContactHasNoRoleOnAccount()
    {
        // Arrange
        SetupHttpContextForController();

        // Drive AuthorizationHelper.SkipRoleCheck -> false
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, _accountId))
            .ReturnsAsync(new List<string>()); // no NoRoleCheckPermissions

        // Drive AuthorizationHelper.CheckRoles -> no NoAccountCheckPermissions bypass, then role == false
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, It.IsAny<int?>()))
            .ReturnsAsync(new List<string>()); // no NoAccountCheckPermissions

        _accountService
            .Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GetSummary(_accountId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSummary_ShouldNotCallCheckRoles_WhenSkipRoleCheckIsTrue()
    {
        // Arrange
        SetupHttpContextForController();

        // Make SkipRoleCheck return true by including permission "COADMI004"
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, _accountId))
            .ReturnsAsync(new List<string> { "COADMI004" });

        _experienceServices
            .Setup(s => s.GetSummaryAsync(_accountId, _contactId, _collabType))
            .ReturnsAsync(new ApiGateway.Models.Summary());

        // Act
        var result = await _sut.GetSummary(_accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify CheckContactRoleAsync was NOT called (optimization)
        _accountService.Verify(
            s => s.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Never,
            "CheckContactRoleAsync should not be called when SkipRoleCheck returns true");
    }

    [Fact]
    public async Task GetSummary_ShouldCallCheckRoles_WhenSkipRoleCheckIsFalse()
    {
        // Arrange
        SetupHttpContextForController();

        // Drive AuthorizationHelper.SkipRoleCheck -> false
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, _accountId))
            .ReturnsAsync(new List<string>()); // no NoRoleCheckPermissions

        // Drive AuthorizationHelper.CheckRoles -> no bypass, role check returns true
        _authorizationService
            .Setup(s => s.GetContactAuthorizationAsync(_contactId, GlobalsConstants.CollaboratorAccountId))
            .ReturnsAsync(new List<string>()); // no NoAccountCheckPermissions

        _accountService
            .Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null))
            .ReturnsAsync(true);

        _experienceServices
            .Setup(s => s.GetSummaryAsync(_accountId, _contactId, _collabType))
            .ReturnsAsync(new ApiGateway.Models.Summary());

        // Act
        var result = await _sut.GetSummary(_accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify CheckContactRoleAsync WAS called when skip is false
        _accountService.Verify(
            s => s.CheckContactRoleAsync(_contactId, _accountId, null),
            Times.Once,
            "CheckContactRoleAsync should be called when SkipRoleCheck returns false");
    }

    [Fact]
    public async Task SendEmailAsync_Nominal()
    {
        _experienceServices.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string?>()))
            .ReturnsAsync([1, 2, 3]);

        var result = await _sut.SendEmailAsync(1, [1, 2, 3], "CLIENT") as OkObjectResult;

        result.Should().BeOfType<OkObjectResult>();
        result!.Value.Should().BeEquivalentTo(new[] { 1, 2, 3 });

        _experienceServices.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string?>()), Times.Once);
    }
}