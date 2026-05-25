using System.Net;
using System.Security.Claims;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using ApiGateway.ProspectExperience.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

public class ProspectExperienceControllerTests
{
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<IFeatureFlagService> _featureFlagService;
    private readonly Mock<IProspectService> _orchestrationService;
    private readonly IValidator<CreateProspectRequest> _validator;
    private readonly Mock<ILogger<ProspectExperienceController>> _logger;
    private readonly ProspectExperienceController _controller;

    private const string UserEmail = "user@test.fr";

    public ProspectExperienceControllerTests()
    {
        _userContext = CreateUserContext(UserEmail);
        _featureFlagService = new Mock<IFeatureFlagService>();
        _orchestrationService = new Mock<IProspectService>();
        _validator = new CreateProspectRequestValidator();
        _logger = new Mock<ILogger<ProspectExperienceController>>();

        _controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _orchestrationService.Object,
            _validator,
            _logger.Object);
    }

    private static Mock<IUserContext> CreateUserContext(string email)
    {
        var claim = new Claim(ClaimTypes.Email, email);
        var principal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        principal.Setup(p => p.FindFirst(It.IsAny<string>())).Returns(claim);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(principal.Object);
        return userContext;
    }

    private static CreateProspectRequest BuildRequest() => new()
    {
        Siret = "12345678901234",
        LegalForm = "SARL",
        LegalStructure = "PERSONNE_MORALE",
        CaseManagerContactId = 100,
        AccountManagerContactId = 200,
        Department = "75",
        Region = "11",
        Country = "FR",
        Signatory = new SignatoryDto
        {
            Title = "M.",
            LastName = "Dupont",
            FirstName = "Jean",
            JobTitle = "Dirigeant",
            Department = "Direction",
            CompanyRole = "Gérant",
            ContactTypes = new[] { "Signataire" },
            Email = "jean.dupont@test.fr",
            MobilePhone = "+33612345678"
        }
    };

    [Fact]
    public async Task CreateProspect_WhenFeatureFlagDisabled_Returns403AndDoesNotCallOrchestration()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateProspect(BuildRequest(), CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.ProspectExperienceDisabledCode,
            ErrorMessage = Errors.ProspectExperienceDisabledMessage
        });
        _orchestrationService.Verify(
            s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProspect_WhenFeatureFlagEnabledAndOrchestrationSucceeds_Returns201WithProspectListItem()
    {
        // Arrange
        var request = BuildRequest();
        var prospect = new ProspectListItem
        {
            AccountId = 43,
            AccountNumber = "AK-001",
            AccountType = ApiGateway.ProspectExperience.Enum.AccountType.PROSPECT,
            LegalName = "ACME SARL",
            Signatory = new ProspectSignatoryListItem
            {
                ContactId = 99,
                FirstName = "Jean",
                LastName = "Dupont",
                Email = "jean.dupont@test.fr"
            },
            StepCode = ProspectStepCode.InProgress
        };

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orchestrationService
            .Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prospect);

        // Act
        var result = await _controller.CreateProspect(request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)HttpStatusCode.Created);
        objectResult.Value.Should().BeSameAs(prospect);
    }

    [Fact]
    public async Task CreateProspect_WhenOrchestrationThrowsProspectOrchestrationException_BubblesUp()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orchestrationService
            .Setup(s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProspectOrchestrationException(step: 4));

        // Act
        Func<Task> act = () => _controller.CreateProspect(BuildRequest(), CancellationToken.None);

        // Assert - the controller does NOT catch; middleware handles it
        await act.Should().ThrowAsync<ProspectOrchestrationException>();
    }

    [Fact]
    public async Task CreateProspect_WhenSiretAlreadyExists_BubblesUp()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orchestrationService
            .Setup(s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SiretAlreadyExistsException("12345678901234"));

        // Act
        Func<Task> act = () => _controller.CreateProspect(BuildRequest(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<SiretAlreadyExistsException>();
    }
}
