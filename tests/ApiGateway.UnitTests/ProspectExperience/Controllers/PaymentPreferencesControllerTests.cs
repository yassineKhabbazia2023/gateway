using System.Security.Claims;
using ApiGateway.Contact;
using ContactModel = ApiGateway.Contact.Models.Contact;
using ApiGateway.Identity.context;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

/// <summary>
/// Unit tests for <see cref="PaymentPreferencesController"/>.
/// </summary>
public sealed class PaymentPreferencesControllerTests
{
    private readonly Mock<IPaymentPreferencesOrchestrationService> _service = new();
    private readonly Mock<IContactService> _contactService = new();

    /// <summary>
    /// Verifies that GET returns the payment preference payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceExists_ReturnsOk()
    {
        var expected = new PaymentPreferenceResponse { PaymentType = "OTHER" };
        _service
            .Setup(service => service.GetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController("user@test.fr");

        var actionResult = await controller.GetAsync(10, CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Verifies that GET returns not found when the orchestration service has no payload.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPreferenceIsMissing_Returns404()
    {
        _service
            .Setup(service => service.GetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentPreferenceResponse?)null);
        var controller = CreateController("user@test.fr");

        var actionResult = await controller.GetAsync(10, CancellationToken.None);

        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that POST OTHER returns no content after successful orchestration.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenSaved_ReturnsNoContent()
    {
        _service
            .Setup(service => service.SetOtherAsync(10, "user@test.fr", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _contactService
            .Setup(service => service.GetContactAsync("user@test.fr"))
            .ReturnsAsync(new ContactModel { Id = 7, Email = "user@test.fr" });
        var controller = CreateController("user@test.fr");

        var result = await controller.SetOtherAsync(10, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that POST OTHER returns bad request when the authenticated email is blank.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenEmailIsBlank_ReturnsBadRequest()
    {
        var controller = CreateController(" ");

        var result = await controller.SetOtherAsync(10, CancellationToken.None);

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
    /// Verifies that POST OTHER returns not found when orchestration cannot save.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenSaveFails_Returns404()
    {
        _service
            .Setup(service => service.SetOtherAsync(10, "user@test.fr", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _contactService
            .Setup(service => service.GetContactAsync("user@test.fr"))
            .ReturnsAsync(new ContactModel { Id = 7, Email = "user@test.fr" });
        var controller = CreateController("user@test.fr");

        var result = await controller.SetOtherAsync(10, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Verifies that DELETE returns no content after successful orchestration.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenReset_ReturnsNoContent()
    {
        _service
            .Setup(service => service.ResetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = CreateController("user@test.fr");

        var result = await controller.ResetAsync(10, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that DELETE returns not found when orchestration cannot reset.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenResetFails_Returns404()
    {
        _service
            .Setup(service => service.ResetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CreateController("user@test.fr");

        var result = await controller.ResetAsync(10, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Creates a controller with the provided user email claim.
    /// </summary>
    /// <param name="email">The user email claim value.</param>
    /// <returns>The tested controller.</returns>
    private PaymentPreferencesController CreateController(string email)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email)],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(context => context.User).Returns(new ClaimsPrincipal(identity));
        return new PaymentPreferencesController(userContext.Object, _contactService.Object, _service.Object);
    }
}
