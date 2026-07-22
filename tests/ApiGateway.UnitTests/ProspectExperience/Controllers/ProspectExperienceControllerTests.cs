using System.Net;
using System.Reflection;
using System.Security.Claims;
using ApiGateway.Attributes;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Consts;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
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
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

public class ProspectExperienceControllerTests
{
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<IFeatureFlagService> _featureFlagService;
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<IProspectService> _orchestrationService;
    private readonly Mock<IProspectApiClient> _prospectApiClient;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<IAuthorizationService> _authorizationService;
    private readonly Mock<IAccountService> _accountService;
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
        _prospectApiClient = new Mock<IProspectApiClient>();
        _contactService = new Mock<IContactService>();
        _contactService.Setup(service => service.GetContactAsync(UserEmail)).ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = 789, Email = UserEmail });
        _authorizationService = new Mock<IAuthorizationService>();
        _authorizationService.Setup(service => service.GetContactAuthorizationAsync(789, It.IsAny<int?>())).ReturnsAsync(new List<string>());
        _authorizationService.Setup(service => service.GetContactAuthorizationAsync(789, -1)).ReturnsAsync(new List<string> { "COPROS001" });
        _accountService = new Mock<IAccountService>();
        _accountService.Setup(service => service.CheckContactRoleAsync(789, It.IsAny<int?>(), null)).ReturnsAsync(true);
        _validator = new CreateProspectRequestValidator();
        _logger = new Mock<ILogger<ProspectExperienceController>>();
        _identityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(true);

        _controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            _prospectApiClient.Object,
            _validator,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object);
        _controller.ControllerContext = CreateControllerContext();
    }

    private static Mock<IUserContext> CreateUserContext(string email, bool isCollaborator = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        if (isCollaborator)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Collaborator"));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role, "Customer"));
        }
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(principal);
        return userContext;
    }

    /// <summary>
    /// Creates a controller context containing services required by <see cref="ApiGateway.Helpers.AuthorizationHelper"/>.
    /// </summary>
    /// <returns>The controller context.</returns>
    private ControllerContext CreateControllerContext(ClaimsPrincipal? user = null)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? _userContext.Object.User,
                RequestServices = new ServiceCollection()
                    .AddControllers()
                    .Services
                    .AddSingleton(_authorizationService.Object)
                    .AddSingleton(_accountService.Object)
                    .BuildServiceProvider()
            }
        };
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
    public async Task CreateProspect_WhenValidationFails_Returns422WithDetailedMessageAndDoesNotCallOrchestration()
    {
        // Arrange
        var request = BuildRequest();
        request.Siret = string.Empty;
        request.LegalForm = string.Empty;

        // Act
        var result = await _controller.CreateProspect(request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var errorResponse = objectResult.Value.Should().BeOfType<Pulse.ExceptionMiddleware.Model.ErrorResponse>().Subject;
        errorResponse.ErrorCode.Should().Be(Errors.ProspectBadRequestCode);
        errorResponse.ErrorMessage.Should().Contain(Errors.ProspectBadRequestMessage);
        errorResponse.ErrorMessage.Should().Contain("Le SIRET est requis.");
        errorResponse.ErrorMessage.Should().Contain("La forme juridique est requise.");
        _orchestrationService.Verify(
            s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProspect_WhenSignatoryEmailUsesRydgeDomain_Returns422WithSpecificErrorCode()
    {
        // Arrange
        var request = BuildRequest();
        request.Signatory.Email = "jean.dupont@rydge.fr";

        // Act
        var result = await _controller.CreateProspect(request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var errorResponse = objectResult.Value.Should().BeOfType<Pulse.ExceptionMiddleware.Model.ErrorResponse>().Subject;
        errorResponse.ErrorCode.Should().Be(Errors.ProspectSignatoryEmailDomainForbiddenCode);
        errorResponse.ErrorMessage.Should().Contain(Errors.ProspectSignatoryEmailDomainForbiddenMessage);
        _orchestrationService.Verify(
            s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProspect_WhenFeatureFlagDisabled_Returns403AndDoesNotCallOrchestration()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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

    /// <summary>
    /// Verifies that a collaborator can be selected as both account manager and case manager.
    /// </summary>
    [Fact]
    public async Task CreateProspect_WhenSameCollaboratorHasBothRoles_CallsOrchestration()
    {
        var request = BuildRequest();
        request.CaseManagerContactId = request.AccountManagerContactId;

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orchestrationService
            .Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectListItem
            {
                AccountId = 43,
                AccountNumber = "AK-001",
                AccountType = AccountType.PROSPECT,
                LegalName = "ACME SARL",
                StepCode = ProspectStepCode.InProgress
            });

        var result = await _controller.CreateProspect(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be((int)HttpStatusCode.Created);
        _orchestrationService.Verify(s => s.CreateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProspect_WhenOrchestrationThrowsProspectOrchestrationException_BubblesUp()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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
    public async Task CreateProspect_WhenIdentityServiceDoesNotValidateCollaborator_Returns403AndDoesNotCallOrchestration()
    {
        // Arrange
        _identityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(false);

        // Act
        var result = await _controller.CreateProspect(BuildRequest(), CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.NotValidCollaboratorCode,
            ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, UserEmail)
        });
        _featureFlagService.Verify(
            f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orchestrationService.Verify(
            s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProspect_WhenUserIsNotCollaborator_Returns403AndDoesNotCallOrchestration()
    {
        // Arrange
        var nonCollaboratorUserContext = CreateUserContext(UserEmail, isCollaborator: false);
        var controller = new ProspectExperienceController(
            nonCollaboratorUserContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            _prospectApiClient.Object,
            _validator,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object)
        {
            ControllerContext = CreateControllerContext(nonCollaboratorUserContext.Object.User)
        };

        // Act
        var result = await controller.CreateProspect(BuildRequest(), CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.NotValidCollaboratorCode,
            ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, UserEmail)
        });
        _orchestrationService.Verify(
            s => s.CreateAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProspect_WhenSiretAlreadyExists_BubblesUp()
    {
        // Arrange
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
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
        const int accountId = 123;
        var request = new CompleteStepRequest { StepName = "Beneficiary" };
        var response = new DocumentUploadResultResponse([1, 2], [3]);

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService
            .Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>()))
            .Returns(true);
        _orchestrationService
            .Setup(service => service.CompleteStepAsync(accountId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.CompleteStepAsync(accountId, request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        objectResult.Value.Should().BeSameAs(response);
        _orchestrationService.Verify(service => service.CompleteStepAsync(accountId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospectApiClient.Verify(
            client => client.GetAccountIdByProspectIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that step completion returns the authorization error before orchestration.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenConnectedContactHasNoAccountRole_ReturnsForbidden()
    {
        const int accountId = 123;
        var request = new CompleteStepRequest { StepName = "Beneficiary" };

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService
            .Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>()))
            .Returns(true);
        _accountService
            .Setup(service => service.CheckContactRoleAsync(789, accountId, null))
            .ReturnsAsync(false);

        var result = await _controller.CompleteStepAsync(accountId, request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _orchestrationService.Verify(
            service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteStepAsync_WhenCallerIsNotCollaborator_Returns403()
    {
        // Arrange
        const int accountId = 123;

        _identityService
            .Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>()))
            .Returns(false);

        // Act
        var result = await _controller.CompleteStepAsync(accountId, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

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
    public async Task CompleteStepAsync_WhenAccountIdIsInvalid_Returns400()
    {
        var result = await _controller.CompleteStepAsync(0, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Result.Should().BeOfType<ObjectResult>().Which.Value.Should().BeOfType<ProblemDetails>().Which.Title.Should().Be("AccountId must be greater than zero.");
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a disabled prospect experience feature flag blocks completion before orchestration.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenFeatureFlagDisabled_Returns403AndDoesNotCallOrchestration()
    {
        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CompleteStepAsync(123, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _orchestrationService.Verify(service => service.CompleteStepAsync(It.IsAny<int>(), It.IsAny<CompleteStepRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #region UploadSupportingDocumentAsync Tests

    /// <summary>
    /// Verifies that a valid request with feature flag enabled, contact found, and prospect ID resolved
    /// returns 201 Created with no response body.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_ValidRequest_Returns201()
    {
        // Arrange
        const int accountId = 456;
        const string documentType = "KBIS";
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");
        mockFile.Setup(f => f.Length).Returns(1024);

        var contact = new ApiGateway.Contact.Models.Contact { Id = 789, Email = UserEmail };
        const int prospectId = 123;

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContactService = new Mock<IContactService>();
        mockContactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync(contact);

        var mockProspectApiClient = new Mock<IProspectApiClient>();
        mockProspectApiClient
            .Setup(c => c.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prospectId);

        _orchestrationService
            .Setup(s => s.UploadSupportingDocumentAsync(
                accountId,
                prospectId,
                contact.Id,
                UserEmail,
                It.Is<UploadSupportingDocumentRequest>(r => r.DocumentType == documentType && r.File == mockFile.Object),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            mockProspectApiClient.Object,
            _validator,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object);
        controller.ControllerContext = CreateControllerContext();

        // Act
        var result = await controller.UploadSupportingDocumentAsync(accountId, documentType, mockFile.Object, CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<StatusCodeResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        _orchestrationService.Verify(
            s => s.UploadSupportingDocumentAsync(
                accountId,
                prospectId,
                contact.Id,
                UserEmail,
                It.Is<UploadSupportingDocumentRequest>(r => r.DocumentType == documentType && r.File == mockFile.Object),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that an invalid accountId (less than or equal to zero) returns 400 Bad Request.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task UploadSupportingDocumentAsync_InvalidAccountId_Returns400(int invalidAccountId)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");

        // Act
        var result = await _controller.UploadSupportingDocumentAsync(invalidAccountId, "KBIS", mockFile.Object, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("AccountId must be greater than zero.");

        _orchestrationService.Verify(
            s => s.UploadSupportingDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<UploadSupportingDocumentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that a disabled prospect experience feature flag returns 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_FeatureFlagDisabled_Returns403()
    {
        // Arrange
        const int accountId = 456;
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UploadSupportingDocumentAsync(accountId, "KBIS", mockFile.Object, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.ProspectExperienceDisabledCode,
            ErrorMessage = Errors.ProspectExperienceDisabledMessage
        });

        _orchestrationService.Verify(
            s => s.UploadSupportingDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<UploadSupportingDocumentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when contact is not found, the endpoint returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_ContactNotFound_Returns404()
    {
        // Arrange
        const int accountId = 456;
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContactService = new Mock<IContactService>();
        mockContactService
            .Setup(service => service.GetContactAsync(UserEmail))
            .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);

        var controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            new Mock<IProspectApiClient>().Object,
            _validator,
            mockContactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object);
        controller.ControllerContext = CreateControllerContext();

        // Act
        var result = await controller.UploadSupportingDocumentAsync(accountId, "KBIS", mockFile.Object, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();

        _orchestrationService.Verify(
            s => s.UploadSupportingDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<UploadSupportingDocumentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when no active prospect is found for the given accountId, the endpoint returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_AccountIdNotFound_Returns404()
    {
        // Arrange
        const int accountId = 456;
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");

        var contact = new ApiGateway.Contact.Models.Contact { Id = 789, Email = UserEmail };

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContactService = new Mock<IContactService>();
        mockContactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync(contact);

        var mockProspectApiClient = new Mock<IProspectApiClient>();
        mockProspectApiClient
            .Setup(c => c.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            mockProspectApiClient.Object,
            _validator,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object);
        controller.ControllerContext = CreateControllerContext();

        // Act
        var result = await controller.UploadSupportingDocumentAsync(accountId, "KBIS", mockFile.Object, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();

        _orchestrationService.Verify(
            s => s.UploadSupportingDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<UploadSupportingDocumentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that upload succeeds and returns 201 Created with no response body.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_ValidRequest_Returns201_NoBody()
    {
        // Arrange
        const int accountId = 456;
        const string documentType = "STATUTS";
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("statuts.pdf");
        mockFile.Setup(f => f.Length).Returns(2048);

        var contact = new ApiGateway.Contact.Models.Contact { Id = 789, Email = UserEmail };
        const int prospectId = 123;

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockContactService = new Mock<IContactService>();
        mockContactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync(contact);

        var mockProspectApiClient = new Mock<IProspectApiClient>();
        mockProspectApiClient
            .Setup(c => c.GetProspectIdByAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prospectId);

        _orchestrationService
            .Setup(s => s.UploadSupportingDocumentAsync(
                accountId,
                prospectId,
                contact.Id,
                UserEmail,
                It.Is<UploadSupportingDocumentRequest>(r => r.DocumentType == documentType && r.File == mockFile.Object),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new ProspectExperienceController(
            _userContext.Object,
            _featureFlagService.Object,
            _identityService.Object,
            _orchestrationService.Object,
            mockProspectApiClient.Object,
            _validator,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            new Mock<IEngagementLetterOrchestrationService>().Object,
            _logger.Object);
        controller.ControllerContext = CreateControllerContext();

        // Act
        var result = await controller.UploadSupportingDocumentAsync(accountId, documentType, mockFile.Object, CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<StatusCodeResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Verifies that supporting document upload declares the same permission filter pattern as the exposed Prospect endpoints.
    /// </summary>
    [Fact]
    public void UploadSupportingDocumentAsync_ShouldRequireProspectDocumentPermissions()
    {
        var method = typeof(ProspectExperienceController).GetMethod(nameof(ProspectExperienceController.UploadSupportingDocumentAsync));

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.CheckAccountRole.Should().BeFalse();
        GetRequiredPermissions(attribute).Should().BeEquivalentTo(
            PermissionCodes.ProspectOnboardingCollaboratorAccess,
            PermissionCodes.ProspectDocumentConsultationClientAccess,
            PermissionCodes.ProspectConfigurationConsultationClientAccess);
    }

    /// <summary>
    /// Verifies that supporting document upload still enforces the existing account role check inside the action.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenCallerHasNoAccountRole_Returns403AndDoesNotCallDownstream()
    {
        const int accountId = 456;
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("document.pdf");
        mockFile.Setup(f => f.Length).Returns(1024);

        _featureFlagService
            .Setup(f => f.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _accountService
            .Setup(service => service.CheckContactRoleAsync(789, accountId, null))
            .ReturnsAsync(false);

        var result = await _controller.UploadSupportingDocumentAsync(accountId, "KBIS", mockFile.Object, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _prospectApiClient.Verify(
            client => client.GetProspectIdByAccountIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orchestrationService.Verify(
            service => service.UploadSupportingDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<UploadSupportingDocumentRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    /// <summary>
    /// Reads the permission codes configured on a <see cref="RequirePermissionAttribute"/>.
    /// </summary>
    /// <param name="attribute">The permission attribute to inspect.</param>
    /// <returns>The permission codes declared on the attribute.</returns>
    private static string[] GetRequiredPermissions(RequirePermissionAttribute attribute)
    {
        var permissionsField = typeof(RequirePermissionAttribute)
            .GetField("_permissions", BindingFlags.Instance | BindingFlags.NonPublic);

        return (string[])permissionsField!.GetValue(attribute)!;
    }
}

