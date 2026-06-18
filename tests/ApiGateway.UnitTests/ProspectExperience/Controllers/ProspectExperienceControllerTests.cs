using System.Net;
using System.Security.Claims;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using ApiGateway.ProspectExperience.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

public class ProspectExperienceControllerTests
{
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<IFeatureFlagService> _featureFlagService;
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<IProspectService> _orchestrationService;
    private readonly IValidator<CreateProspectRequest> _validator;
    private readonly Mock<ILogger<ProspectExperienceController>> _logger;
    private readonly ProspectExperienceController _controller;

    private const string UserEmail = "user@test.fr";

    public ProspectExperienceControllerTests()
    {
        _userContext = CreateUserContext(UserEmail);
        _featureFlagService = new Mock<IFeatureFlagService>();
        _identityService = new Mock<IIdentityService>();
        _orchestrationService = new Mock<IProspectService>();
        _validator = new CreateProspectRequestValidator();
        _logger = new Mock<ILogger<ProspectExperienceController>>();
        _identityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(true);

        _controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            _validator,
            _logger.Object);
    }

    private static Mock<IUserContext> CreateUserContext(string email)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "Collaborator")
            },
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(principal);
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

    [Fact]
    public async Task CompleteStepAsync_WhenFeatureFlagEnabledAndCollaborator_ReturnsOk()
    {
        // Arrange
        const int prospectId = 123;
        var request = new CompleteStepRequest { StepName = "Beneficiary" };
        var response = new DocumentUploadResultResponse([1, 2], [3]);

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService
            .Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>()))
            .Returns(true);
        _orchestrationService
            .Setup(service => service.CompleteStepAsync(prospectId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.CompleteStepAsync(prospectId, request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        objectResult.Value.Should().BeSameAs(response);
        _orchestrationService.Verify(service => service.CompleteStepAsync(prospectId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteStepAsync_WhenCallerIsNotCollaborator_Returns403()
    {
        // Arrange
        const int prospectId = 123;

        _identityService
            .Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>()))
            .Returns(false);

        // Act
        var result = await _controller.CompleteStepAsync(prospectId, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.NotValidCollaboratorCode,
            ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, UserEmail)
        });
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteStepAsync_WhenStepNameIsMissing_Returns400()
    {
        var result = await _controller.CompleteStepAsync(123, new CompleteStepRequest { StepName = " " }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a missing request body returns 400 without calling orchestration.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenRequestBodyIsMissing_Returns400()
    {
        var result = await _controller.CompleteStepAsync(123, null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that an invalid prospect identifier returns 400 without calling orchestration.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenProspectIdIsInvalid_Returns400()
    {
        var result = await _controller.CompleteStepAsync(0, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a disabled prospect experience feature flag blocks completion before orchestration.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenFeatureFlagDisabled_Returns403AndDoesNotCallOrchestration()
    {
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CompleteStepAsync(123, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
