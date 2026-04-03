using System.Net;
using System.Security.Claims;
using ApiGateway.Booking;
using ApiGateway.Contact;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Booking;

public class BookingControllerTests
{
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<IBookingExperienceGuards> _bookingGuards;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<IBookingSyncTrigger> _syncTrigger;
    private readonly Mock<ILogger<BookingController>> _logger;
    private readonly BookingController _controller;

    private const string UserEmail = "user@test.fr";

    public BookingControllerTests()
    {
        _userContext = CreateUserContext(UserEmail);
        _bookingGuards = new Mock<IBookingExperienceGuards>();
        _contactService = new Mock<IContactService>();
        _syncTrigger = new Mock<IBookingSyncTrigger>();
        _logger = new Mock<ILogger<BookingController>>();

        _controller = new BookingController(
            _userContext.Object,
            _bookingGuards.Object,
            _contactService.Object,
            _syncTrigger.Object,
            _logger.Object
        );
    }

    private static Mock<IUserContext> CreateUserContext(string email)
    {
        var claim = new Claim(ClaimValueTypes.Email, email);
        var userClaimPrincipal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(userClaimPrincipal.Object);
        userContext.Setup(f => f.User.FindFirst(It.IsAny<string>())).Returns(claim);
        return userContext;
    }

    private void SetupGuardsAllowed()
    {
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(true);
    }

    private void SetupContactFound(int contactId = 123)
    {
        _contactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = contactId });
    }

    #region GetBookingAccess Tests

    [Fact]
    public async Task GetBookingAccess_FeatureFlagEnabled_UserHasAccess_ReturnsHasAccessTrue()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(true);
        SetupContactFound();

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = true });
    }

    [Fact]
    public async Task GetBookingAccess_FeatureFlagDisabled_UserHasAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(true);

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }

    [Fact]
    public async Task GetBookingAccess_FeatureFlagEnabled_UserHasNoAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        var userContext = CreateUserContext("notlisted@test.fr");
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        bookingGuards.Setup(s => s.HasAccess("notlisted@test.fr")).Returns(false);
        var controller = new BookingController(
            userContext.Object,
            bookingGuards.Object,
            _contactService.Object,
            _syncTrigger.Object,
            _logger.Object
        );

        // Act
        var result = await controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }

    [Fact]
    public async Task GetBookingAccess_FeatureFlagDisabled_UserHasNoAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(false);

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }

    [Fact]
    public async Task GetBookingAccess_WhenHasAccess_ResolvesContactAndTriggersSyncIfNeeded()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound(contactId: 123);
        _syncTrigger.Setup(s => s.ShouldTriggerSync(123)).Returns(true);

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.Value.Should().BeEquivalentTo(new { hasAccess = true });
        _contactService.Verify(s => s.GetContactAsync(UserEmail), Times.Once);
        _syncTrigger.Verify(s => s.ShouldTriggerSync(123), Times.Once);
        _syncTrigger.Verify(s => s.TriggerSync(123), Times.Once);
    }

    [Fact]
    public async Task GetBookingAccess_WhenHasAccess_AndContactNotFound_ReturnsHasAccessTrue()
    {
        // Arrange
        SetupGuardsAllowed();
        _contactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = true });
        _syncTrigger.Verify(s => s.ShouldTriggerSync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBookingAccess_WhenHasAccess_AndContactResolutionFails_ReturnsHasAccessTrue()
    {
        // Arrange
        SetupGuardsAllowed();
        _contactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ThrowsAsync(new Exception("Contact API down"));

        // Act
        var result = await _controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = true });
    }

    [Fact]
    public async Task GetBookingAccess_WhenNoAccess_DoesNotResolveContact()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(false);

        // Act
        await _controller.GetBookingAccess();

        // Assert
        _contactService.Verify(s => s.GetContactAsync(It.IsAny<string>()), Times.Never);
        _syncTrigger.Verify(s => s.ShouldTriggerSync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBookingAccess_WhenShouldTriggerSyncFalse_DoesNotCallTriggerSync()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound(contactId: 123);
        _syncTrigger.Setup(s => s.ShouldTriggerSync(123)).Returns(false);

        // Act
        await _controller.GetBookingAccess();

        // Assert
        _syncTrigger.Verify(s => s.ShouldTriggerSync(123), Times.Once);
        _syncTrigger.Verify(s => s.TriggerSync(It.IsAny<int>()), Times.Never);
    }

    #endregion
}
