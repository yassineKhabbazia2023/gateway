using System.Net;
using System.Security.Claims;
using ApiGateway.Contact;
using ApiGateway.FeatureFlags;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Services;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

public class EngagementLetterControllerTests
{
    private readonly Mock<IEngagementLetterOrchestrationService> _orchestrationService;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<ILogger<ProspectExperienceController>> _logger;
    private readonly ProspectExperienceController _controller;

    private const int AccountId = 42;
    private const string UserEmail = "collab@test.fr";
    private static readonly ApiGateway.Contact.Models.Contact CurrentContact = new() { Id = 7, Email = UserEmail };

    public EngagementLetterControllerTests()
    {
        _orchestrationService = new Mock<IEngagementLetterOrchestrationService>(MockBehavior.Strict);
        _contactService = new Mock<IContactService>(MockBehavior.Strict);
        _userContext = CreateUserContext(UserEmail);
        _logger = new Mock<ILogger<ProspectExperienceController>>();

        _controller = new ProspectExperienceController(
            _userContext.Object,
            new Mock<IFeatureFlagService>().Object,
            new Mock<IIdentityService>().Object,
            new Mock<IProspectService>().Object,
            new Mock<IProspectApiClient>().Object,
            new Mock<IValidator<CreateProspectRequest>>().Object,
            _contactService.Object,
            new Mock<ICommercialProposalOrchestrationService>().Object,
            _orchestrationService.Object,
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

    private static IFormFile BuildFile(long length = 1024, string contentType = "application/pdf")
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.FileName).Returns("engagement-letter.pdf");
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length > 0 ? (int)length : 1]));
        return file.Object;
    }

    // ---- SendEngagementLetterAsync ----

    [Fact]
    public async Task SendEngagementLetterAsync_WhenFileIsNull_ReturnsValidationProblem()
    {
        var result = await _controller.SendEngagementLetterAsync(AccountId, null!, CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenFileIsEmpty_ReturnsValidationProblem()
    {
        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(length: 0), CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenFileIsNotPdf_Returns415()
    {
        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(contentType: "image/png"), CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(415);
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenFileExceedsMaxSize_Returns413()
    {
        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(length: 11 * 1024 * 1024), CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenContactNotFound_Returns404()
    {
        _contactService.Setup(s => s.GetContactAsync(UserEmail)).ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);

        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenOutcomeIsSent_Returns201()
    {
        _contactService.Setup(s => s.GetContactAsync(UserEmail)).ReturnsAsync(CurrentContact);
        _orchestrationService
            .Setup(s => s.SendAsync(AccountId, CurrentContact.Id, UserEmail, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngagementLetterOrchestrationOutcome.Sent);

        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(), CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be((int)HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenOutcomeIsProspectNotFound_Returns404()
    {
        _contactService.Setup(s => s.GetContactAsync(UserEmail)).ReturnsAsync(CurrentContact);
        _orchestrationService
            .Setup(s => s.SendAsync(AccountId, CurrentContact.Id, UserEmail, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngagementLetterOrchestrationOutcome.ProspectNotFound);

        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenOutcomeIsAlreadySent_Returns409()
    {
        _contactService.Setup(s => s.GetContactAsync(UserEmail)).ReturnsAsync(CurrentContact);
        _orchestrationService
            .Setup(s => s.SendAsync(AccountId, CurrentContact.Id, UserEmail, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngagementLetterOrchestrationOutcome.AlreadySent);

        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SendEngagementLetterAsync_WhenOutcomeIsNotEligible_Returns422()
    {
        _contactService.Setup(s => s.GetContactAsync(UserEmail)).ReturnsAsync(CurrentContact);
        _orchestrationService
            .Setup(s => s.SendAsync(AccountId, CurrentContact.Id, UserEmail, It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngagementLetterOrchestrationOutcome.NotEligible);

        var result = await _controller.SendEngagementLetterAsync(AccountId, BuildFile(), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be((int)HttpStatusCode.UnprocessableEntity);
    }
}
