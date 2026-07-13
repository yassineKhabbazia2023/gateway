using System.Security.Claims;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

/// <summary>
/// Unit tests for <see cref="OnboardingMaintenanceController"/>.
/// </summary>
public sealed class OnboardingMaintenanceControllerTests
{
    private const string UserEmail = "collab@test.fr";
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IFeatureFlagService> _featureFlagService = new();
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<IContactService> _contactService = new();
    private readonly Mock<IProspectApiClient> _prospectApiClient = new();
    private readonly Mock<IMandatePaymentPreferencesClient> _mandateClient = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IAccountService> _accountService = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingMaintenanceControllerTests"/> class.
    /// </summary>
    public OnboardingMaintenanceControllerTests()
    {
        _userContext.SetupGet(context => context.User).Returns(CreateCollaborator(UserEmail));
        _identityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(true);
        _contactService.Setup(service => service.GetContactAsync(UserEmail)).ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = 789, Email = UserEmail });
        _authorizationService.Setup(service => service.GetContactAuthorizationAsync(789, It.IsAny<int?>())).ReturnsAsync(new List<string>());
        _accountService.Setup(service => service.CheckContactRoleAsync(789, It.IsAny<int?>(), null)).ReturnsAsync(true);
    }

    #region CleanupAsync

    /// <summary>
    /// Verifies that cleanup rejects invalid account identifiers.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenAccountIdIsInvalid_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.CleanupAsync(0, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _featureFlagService.Verify(
            service => service.IsEnabledAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that cleanup is forbidden when the feature flag is disabled.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenFeatureFlagDisabled_ReturnsForbiddenAndDoesNotCallDownstream()
    {
        var controller = CreateController();
        SetupFeatureFlag(false);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _prospectApiClient.Verify(client => client.CleanupOnboardingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _mandateClient.Verify(client => client.CleanupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that cleanup is forbidden when the token is not a valid collaborator token.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenUserIsNotValidCollaborator_ReturnsForbidden()
    {
        _identityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(false);
        var controller = CreateController();
        SetupFeatureFlag(true);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Verifies that cleanup is forbidden when the connected user is not a collaborator.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenUserIsNotCollaborator_ReturnsForbidden()
    {
        _userContext.SetupGet(context => context.User).Returns(CreateUserWithoutCollaboratorRole(UserEmail));
        var controller = CreateController();
        SetupFeatureFlag(true);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _prospectApiClient.Verify(client => client.CleanupOnboardingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that cleanup returns forbidden when the collaborator has no role on the account.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenCollaboratorHasNoAccountRole_ReturnsForbidden()
    {
        var controller = CreateController();
        SetupFeatureFlag(true);
        _authorizationService.Setup(service => service.GetContactAuthorizationAsync(789, It.IsAny<int?>())).ReturnsAsync(["OTHER"]);
        _accountService.Setup(service => service.CheckContactRoleAsync(789, It.IsAny<int?>(), null)).ReturnsAsync(false);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _prospectApiClient.Verify(client => client.CleanupOnboardingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that cleanup returns not found and does not call Mandate when Prospect is missing.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenProspectIsMissing_ReturnsNotFoundAndDoesNotCallMandate()
    {
        var controller = CreateController();
        SetupFeatureFlag(true);
        _prospectApiClient.Setup(client => client.CleanupOnboardingAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _mandateClient.Verify(client => client.CleanupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that cleanup returns not found when Mandate cannot clean the account.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenMandateIsMissing_ReturnsNotFound()
    {
        var controller = CreateController();
        SetupFeatureFlag(true);
        _prospectApiClient.Setup(client => client.CleanupOnboardingAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mandateClient.Setup(client => client.CleanupAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _prospectApiClient.Verify(client => client.CleanupOnboardingAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _mandateClient.Verify(client => client.CleanupAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that cleanup calls both downstream services and returns no content.
    /// </summary>
    [Fact]
    public async Task CleanupAsync_WhenDownstreamsClean_ReturnsNoContent()
    {
        var controller = CreateController();
        SetupFeatureFlag(true);
        _prospectApiClient.Setup(client => client.CleanupOnboardingAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mandateClient.Setup(client => client.CleanupAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await controller.CleanupAsync(42, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _prospectApiClient.Verify(client => client.CleanupOnboardingAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _mandateClient.Verify(client => client.CleanupAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    /// <summary>
    /// Creates the controller under test.
    /// </summary>
    /// <returns>The controller.</returns>
    private OnboardingMaintenanceController CreateController()
    {
        var services = new ServiceCollection()
            .AddSingleton(_authorizationService.Object)
            .AddSingleton(_accountService.Object)
            .BuildServiceProvider();

        return new OnboardingMaintenanceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _contactService.Object,
            _prospectApiClient.Object,
            _mandateClient.Object,
            NullLogger<OnboardingMaintenanceController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services
                }
            }
        };
    }

    /// <summary>
    /// Configures the QA-sensitive feature flag result.
    /// </summary>
    /// <param name="enabled">A value indicating whether the flag is enabled.</param>
    private void SetupFeatureFlag(bool enabled)
    {
        _featureFlagService
            .Setup(service => service.IsEnabledAsync(
                FeatureFlagKeys.IsQaSensitiveEndpointsEnabled,
                It.IsAny<bool>(),
                It.IsAny<FeatureContext?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabled);
    }

    /// <summary>
    /// Creates a collaborator principal.
    /// </summary>
    /// <param name="email">The collaborator email.</param>
    /// <returns>The claims principal.</returns>
    private static ClaimsPrincipal CreateCollaborator(string email)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "Collaborator")
            ],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a non-collaborator principal.
    /// </summary>
    /// <param name="email">The user email.</param>
    /// <returns>The claims principal.</returns>
    private static ClaimsPrincipal CreateUserWithoutCollaboratorRole(string email)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email)],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
