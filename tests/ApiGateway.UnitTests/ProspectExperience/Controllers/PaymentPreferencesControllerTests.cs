using System.Reflection;
using System.Security.Claims;
using ApiGateway.Account;
using ApiGateway.Attributes;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Consts;
using ApiGateway.Contact;
using ContactModel = ApiGateway.Contact.Models.Contact;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

/// <summary>
/// Unit tests for <see cref="PaymentPreferencesController"/>.
/// </summary>
public sealed class PaymentPreferencesControllerTests
{
    // The route now carries the account identifier, which the controller resolves to the prospect
    // identifier before delegating to the orchestration service. Distinct values prove the mapping.
    private const int AccountId = 20;
    private const int ProspectId = 10;

    private readonly Mock<IPaymentPreferencesOrchestrationService> _service = new();
    private readonly Mock<IContactService> _contactService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IAccountService> _accountService = new();
    private readonly Mock<IProspectApiClient> _prospectApiClient = new();

    /// <summary>
    /// Verifies that GET returns the payment preference payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceExists_ReturnsOk()
    {
        var expected = new PaymentPreferenceResponse { PaymentType = "OTHER" };
        _service
            .Setup(service => service.GetAsync(ProspectId, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController("user@test.fr");

        var actionResult = await controller.GetAsync(AccountId, CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Verifies that GET returns not found when the account cannot be resolved to a prospect.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenAccountIsNotResolved_Returns404()
    {
        var controller = CreateController("user@test.fr");
        _prospectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var actionResult = await controller.GetAsync(AccountId, CancellationToken.None);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
        _service.Verify(
            service => service.GetAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that GET returns not found when the orchestration service has no payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceIsMissing_Returns404()
    {
        _service
            .Setup(service => service.GetAsync(ProspectId, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentPreferenceResponse?)null);
        var controller = CreateController("user@test.fr");

        var actionResult = await controller.GetAsync(AccountId, CancellationToken.None);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that GET returns the authorization error when the connected contact has no role on the account.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenConnectedContactHasNoAccountRole_ReturnsForbidden()
    {
        var controller = CreateController("user@test.fr");
        SetupNoAccountRole();

        var actionResult = await controller.GetAsync(AccountId, CancellationToken.None);

        actionResult.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(
            service => service.GetAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that signed SEPA mandate download returns the binary content with its document content type.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenDocumentExists_ReturnsDocumentContentType()
    {
        _service
            .Setup(service => service.DownloadSignedSepaMandateAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([4, 5, 6], "application/pdf", "signed.pdf"));
        var controller = CreateController("user@test.fr");

        var result = await controller.DownloadSignedSepaMandateAsync(AccountId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("signed.pdf");
        file.FileContents.Should().Equal([4, 5, 6]);
    }

    /// <summary>
    /// Verifies that signed SEPA mandate download returns not found when unavailable.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenDocumentIsUnavailable_Returns404()
    {
        _service
            .Setup(service => service.DownloadSignedSepaMandateAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectDocumentContentResponse?)null);
        var controller = CreateController("user@test.fr");

        var result = await controller.DownloadSignedSepaMandateAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that signed SEPA mandate download returns the authorization error before downstream calls.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenConnectedContactHasNoAccountRole_ReturnsForbidden()
    {
        var controller = CreateController("user@test.fr");
        SetupNoAccountRole();

        var result = await controller.DownloadSignedSepaMandateAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(
            service => service.DownloadSignedSepaMandateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER returns no content after successful orchestration.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenSaved_ReturnsNoContent()
    {
        _service
            .Setup(service => service.SetOtherAsync(ProspectId, "user@test.fr", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController("user@test.fr");

        var result = await controller.SetOtherAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that POST OTHER returns bad request when the authenticated email is blank.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenEmailIsBlank_ReturnsBadRequest()
    {
        var controller = CreateController(" ");

        var result = await controller.SetOtherAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(
            service => service.SetOtherAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER returns not found when the account cannot be resolved to a prospect.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenAccountIsNotResolved_Returns404()
    {
        var controller = CreateController("user@test.fr");
        _prospectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await controller.SetOtherAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _service.Verify(
            service => service.SetOtherAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER returns not found when orchestration cannot save.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenSaveFails_Returns404()
    {
        _service
            .Setup(service => service.SetOtherAsync(ProspectId, "user@test.fr", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController("user@test.fr");

        var result = await controller.SetOtherAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that POST OTHER returns the authorization error before downstream calls.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenConnectedContactHasNoAccountRole_ReturnsForbidden()
    {
        var controller = CreateController("user@test.fr");
        SetupNoAccountRole();

        var result = await controller.SetOtherAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(
            service => service.SetOtherAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA returns the signature URL when the connected signatory is authorized.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedUserIsSignatory_ReturnsSignatureUrl()
    {
        var request = CreateSepaRequest();
        _service
            .Setup(service => service.SetSepaAsync(
                ProspectId,
                request,
                "user@test.fr",
                "Jean",
                "Dupont",
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SepaPaymentPreferenceOrchestrationResult.Completed("https://signature.test"));
        var controller = CreateController("user@test.fr");

        var result = await controller.SetSepaAsync(AccountId, request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SepaPaymentPreferenceResponse>()
            .Which.SignatureUrl.Should().Be("https://signature.test");
    }

    /// <summary>
    /// Verifies that POST SEPA returns forbidden when the connected user is not the prospect signatory.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedUserIsNotSignatory_ReturnsForbidden()
    {
        var request = CreateSepaRequest();
        _service
            .Setup(service => service.SetSepaAsync(
                ProspectId,
                request,
                "user@test.fr",
                "Jean",
                "Dupont",
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SepaPaymentPreferenceOrchestrationResult.FromOutcome(SepaPaymentPreferenceOrchestrationOutcome.Forbidden));
        var controller = CreateController("user@test.fr");

        var result = await controller.SetSepaAsync(AccountId, request, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    /// Verifies that POST SEPA rejects an empty RIB before reading the connected contact.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenRibFileIsEmpty_ReturnsValidationProblem()
    {
        var request = CreateSepaRequest();
        request.File = new FormFile(new MemoryStream(), 0, 0, "file", "empty.pdf");
        var controller = CreateController("user@test.fr");

        var result = await controller.SetSepaAsync(AccountId, request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeOfType<ValidationProblemDetails>();
        _service.Verify(
            service => service.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<SepaPaymentPreferenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA rejects missing authenticated email before orchestration.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenAuthenticatedEmailIsBlank_ReturnsBadRequest()
    {
        var controller = CreateController(" ");

        var result = await controller.SetSepaAsync(AccountId, CreateSepaRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(
            service => service.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<SepaPaymentPreferenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA returns not found when the account cannot be resolved to a prospect.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenAccountIsNotResolved_ReturnsNotFound()
    {
        var controller = CreateController("user@test.fr");
        _prospectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await controller.SetSepaAsync(AccountId, CreateSepaRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _service.Verify(
            service => service.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<SepaPaymentPreferenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA returns not found when the connected contact cannot be resolved.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedContactIsMissing_ReturnsNotFound()
    {
        var controller = CreateController("user@test.fr");

        _contactService
            .Setup(service => service.GetContactAsync("user@test.fr"))
            .ReturnsAsync((ContactModel?)null);

        var result = await controller.SetSepaAsync(AccountId, CreateSepaRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _service.Verify(
            service => service.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<SepaPaymentPreferenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA rejects connected contacts without first name or last name.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedContactNameIsIncomplete_ReturnsBadRequest()
    {
        var controller = CreateController("user@test.fr");

        _contactService
            .Setup(service => service.GetContactAsync("user@test.fr"))
            .ReturnsAsync(new ContactModel
            {
                Id = 7,
                Email = "user@test.fr",
                FirstName = "Jean",
                LastName = " "
            });

        var result = await controller.SetSepaAsync(AccountId, CreateSepaRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(
            service => service.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<SepaPaymentPreferenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA maps a missing downstream resource to 404.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenOrchestrationReturnsNotFound_Returns404()
    {
        var request = CreateSepaRequest();
        SetupConnectedContact();
        _service
            .Setup(service => service.SetSepaAsync(
                ProspectId,
                request,
                "user@test.fr",
                "Jean",
                "Dupont",
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SepaPaymentPreferenceOrchestrationResult.FromOutcome(SepaPaymentPreferenceOrchestrationOutcome.NotFound));
        var controller = CreateController("user@test.fr");

        var result = await controller.SetSepaAsync(AccountId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that POST SEPA maps Mandat failure to bad gateway.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenMandatFails_ReturnsBadGateway()
    {
        var request = CreateSepaRequest();
        SetupConnectedContact();
        _service
            .Setup(service => service.SetSepaAsync(
                ProspectId,
                request,
                "user@test.fr",
                "Jean",
                "Dupont",
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SepaPaymentPreferenceOrchestrationResult.FromOutcome(SepaPaymentPreferenceOrchestrationOutcome.MandateFailed));
        var controller = CreateController("user@test.fr");

        var result = await controller.SetSepaAsync(AccountId, request, CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    /// <summary>
    /// Verifies that DELETE returns no content after successful orchestration.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenReset_ReturnsNoContent()
    {
        _service
            .Setup(service => service.ResetAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController("user@test.fr");

        var result = await controller.ResetAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that DELETE returns not found when the account cannot be resolved to a prospect.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenAccountIsNotResolved_Returns404()
    {
        var controller = CreateController("user@test.fr");
        _prospectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await controller.ResetAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _service.Verify(
            service => service.ResetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that DELETE returns not found when orchestration cannot reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenResetFails_Returns404()
    {
        _service
            .Setup(service => service.ResetAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController("user@test.fr");

        var result = await controller.ResetAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that DELETE returns the authorization error before downstream calls.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenConnectedContactHasNoAccountRole_ReturnsForbidden()
    {
        var controller = CreateController("user@test.fr");
        SetupNoAccountRole();

        var result = await controller.ResetAsync(AccountId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(
            service => service.ResetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Creates a controller with the provided user email claim.
    /// </summary>
    /// <param name="email">The user email claim value.</param>
    /// <returns>The tested controller.</returns>
    private PaymentPreferencesController CreateController(string email)
    {
        // By default the account resolves to the prospect; individual tests override this to null
        // to exercise the unresolved-account path.
        _prospectApiClient
            .Setup(client => client.GetProspectIdByAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProspectId);

        _contactService
            .Setup(service => service.GetContactAsync(email))
            .ReturnsAsync(new ContactModel
            {
                Id = 7,
                Email = email,
                FirstName = "Jean",
                LastName = "Dupont"
            });
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(7, AccountId))
            .ReturnsAsync(new List<string>());
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(7, -1))
            .ReturnsAsync(new List<string>());
        _accountService
            .Setup(service => service.CheckContactRoleAsync(7, AccountId, null))
            .ReturnsAsync(true);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email)],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(context => context.User).Returns(new ClaimsPrincipal(identity));
        var controller = new PaymentPreferencesController(
            userContext.Object,
            _contactService.Object,
            _prospectApiClient.Object,
            _service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddControllers()
                    .Services
                    .AddSingleton(_authorizationService.Object)
                    .AddSingleton(_accountService.Object)
                    .BuildServiceProvider()
            }
        };

        return controller;
    }

    /// <summary>
    /// Configures the connected contact lookup with a complete contact.
    /// </summary>
    private void SetupConnectedContact()
    {
        _contactService
            .Setup(service => service.GetContactAsync("user@test.fr"))
            .ReturnsAsync(new ContactModel
            {
                Id = 7,
                Email = "user@test.fr",
                FirstName = "Jean",
                LastName = "Dupont"
            });
    }

    /// <summary>
    /// Configures the connected contact as unauthorized on the prospect account.
    /// </summary>
    private void SetupNoAccountRole()
    {
        _accountService
            .Setup(service => service.CheckContactRoleAsync(7, AccountId, null))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Creates a valid SEPA multipart request.
    /// </summary>
    /// <returns>The request.</returns>
    private static SepaPaymentPreferenceRequest CreateSepaRequest()
    {
        var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        return new SepaPaymentPreferenceRequest
        {
            File = file,
            AccountHolder = "Jean Dupont",
            Address = "10 rue de Paris",
            AddressLine2 = "Batiment A",
            City = "Paris",
            Country = "France",
            PostalCode = "75008",
            Iban = "FR7630006000011234567890189",
            Bic = "AGRIFRPP"
        };
    }

    // ---- RequirePermission attribute coverage ----

    [Theory]
    [InlineData(nameof(PaymentPreferencesController.GetAsync))]
    [InlineData(nameof(PaymentPreferencesController.DownloadSignedSepaMandateAsync))]
    [InlineData(nameof(PaymentPreferencesController.SetOtherAsync))]
    [InlineData(nameof(PaymentPreferencesController.SetSepaAsync))]
    [InlineData(nameof(PaymentPreferencesController.ResetAsync))]
    public void Action_ShouldHaveRequirePermissionAttribute(string methodName)
    {
        var method = typeof(PaymentPreferencesController).GetMethod(methodName);

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();

        attribute.Should().NotBeNull();
        // Role-on-account is already enforced by ProspectAccountAuthorizationHelper in the action body.
        attribute!.CheckAccountRole.Should().BeFalse();
    }
}

