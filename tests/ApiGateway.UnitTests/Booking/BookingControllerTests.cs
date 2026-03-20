using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ApiGateway.Booking;
using ApiGateway.Booking.Models;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.UnitTests.Booking;

public class BookingControllerTests
{
    private readonly Mock<IUserContext> _userContext;
    private readonly Mock<IBookingExperienceGuards> _bookingGuards;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<IBookingProvisioningService> _provisioningService;
    private readonly Mock<IBookingSyncTrigger> _syncTrigger;
    private readonly Mock<ILogger<BookingController>> _logger;
    private readonly BookingController _controller;

    private const string UserEmail = "user@test.fr";

    public BookingControllerTests()
    {
        _userContext = CreateUserContext(UserEmail);
        _bookingGuards = new Mock<IBookingExperienceGuards>();
        _contactService = new Mock<IContactService>();
        _provisioningService = new Mock<IBookingProvisioningService>();
        _syncTrigger = new Mock<IBookingSyncTrigger>();
        _logger = new Mock<ILogger<BookingController>>();

        _controller = new BookingController(
            _userContext.Object,
            _bookingGuards.Object,
            _contactService.Object,
            _provisioningService.Object,
            _syncTrigger.Object,
            _logger.Object
        );

        // Setup HttpContext with Authorization header for provisioning tests
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = new StringValues("Bearer test-jwt-token");
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
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

    private static BookingProvisioningRequest CreateProvisioningRequest() =>
        new()
        {
            DisplayName = "Consultation fiscale",
            Description = "30 minutes",
            IsOnline = true,
            Duration = 30,
            Availability =
            [
                new BookingServiceAvailabilityRequest
                {
                    Day = "monday",
                    TimeSlots =
                    [
                        new BookingServiceTimeSlotRequest { Start = "09:00", End = "12:00" },
                    ],
                },
            ],
        };

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

    private static BookingApiResponse CreateBusinessSuccessResponse(int configurationId = 42)
    {
        var body = new BookingBusinessResponse { Id = configurationId };

        return new BookingApiResponse
        {
            StatusCode = HttpStatusCode.Created,
            IsSuccessStatusCode = true,
            Content = JsonSerializer.Serialize(body),
        };
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
            _provisioningService.Object,
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

    #region ProvisionBookingService - Guards Tests

    [Fact]
    public async Task ProvisionBookingService_WhenFeatureFlagDisabled_Returns403()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.XpBookingFeatureFlagDisabledCode);
    }

    [Fact]
    public async Task ProvisionBookingService_WhenUserNotInWhitelist_Returns403()
    {
        // Arrange
        _bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        _bookingGuards.Setup(s => s.HasAccess(UserEmail)).Returns(false);
        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.XpBookingUnauthorizedCode);
    }

    #endregion

    #region ProvisionBookingService - Contact Resolution Tests

    [Fact]
    public async Task ProvisionBookingService_WhenContactNotFound_Returns400()
    {
        // Arrange
        SetupGuardsAllowed();
        _contactService
            .Setup(s => s.GetContactAsync(UserEmail))
            .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);
        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.NotFoundContactCode);
    }

    #endregion

    #region ProvisionBookingService - Step 1 Tests

    [Fact]
    public async Task ProvisionBookingService_WhenStep1Fails_ReturnsDownstreamError()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    IsSuccessStatusCode = false,
                    Content = "Graph API error",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.BookingProvisioningFailedCode);
        error.ErrorMessage.Should().Contain("Graph API error");
    }

    [Fact]
    public async Task ProvisionBookingService_WhenStep1ReturnsEmptyConfigId_Returns400()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        var body = new BookingBusinessResponse(); // Id = 0 by default
        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.Created,
                    IsSuccessStatusCode = true,
                    Content = JsonSerializer.Serialize(body),
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.BookingProvisioningFailedCode);
        error.ErrorMessage.Should().Contain("Business configuration ID is missing");
    }

    #endregion

    #region ProvisionBookingService - Step 2 Tests

    [Fact]
    public async Task ProvisionBookingService_WhenStep2Fails_ReturnsDownstreamError()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(CreateBusinessSuccessResponse(42));

        _provisioningService
            .Setup(s =>
                s.CreateServiceAsync(
                    "42",
                    123,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                )
            )
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    IsSuccessStatusCode = false,
                    Content = "Service validation error",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var error = result.Value as ErrorResponse;
        error!.ErrorCode.Should().Be(Errors.BookingProvisioningFailedCode);
        error.ErrorMessage.Should().Contain("Service validation error");
    }

    #endregion

    #region ProvisionBookingService - Success Tests

    [Fact]
    public async Task ProvisionBookingService_WhenFullSuccess_Returns201Created()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(CreateBusinessSuccessResponse(42));

        _provisioningService
            .Setup(s =>
                s.CreateServiceAsync(
                    "42",
                    123,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                )
            )
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.Created,
                    IsSuccessStatusCode = true,
                    Content = "{}",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task ProvisionBookingService_WhenBusinessAlreadyExists_And_ServiceCreated_Returns201()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        // Step 1 returns 200 (already configured)
        var body = new BookingBusinessResponse { Id = 10 };

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    IsSuccessStatusCode = true,
                    Content = JsonSerializer.Serialize(body),
                }
            );

        _provisioningService
            .Setup(s =>
                s.CreateServiceAsync(
                    "10",
                    123,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                )
            )
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.Created,
                    IsSuccessStatusCode = true,
                    Content = "{}",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task ProvisionBookingService_WhenServiceAlreadyExists_Returns200()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound();

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(123, It.IsAny<string>()))
            .ReturnsAsync(CreateBusinessSuccessResponse(42));

        _provisioningService
            .Setup(s =>
                s.CreateServiceAsync(
                    "42",
                    123,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                )
            )
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    IsSuccessStatusCode = true,
                    Content = "{}",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        var result = await _controller.ProvisionBookingService(request) as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task ProvisionBookingService_ShouldPassCorrectContactIdToService()
    {
        // Arrange
        SetupGuardsAllowed();
        SetupContactFound(contactId: 999);

        _provisioningService
            .Setup(s => s.CreateBusinessAsync(999, It.IsAny<string>()))
            .ReturnsAsync(CreateBusinessSuccessResponse(50));

        _provisioningService
            .Setup(s =>
                s.CreateServiceAsync(
                    "50",
                    999,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                )
            )
            .ReturnsAsync(
                new BookingApiResponse
                {
                    StatusCode = HttpStatusCode.Created,
                    IsSuccessStatusCode = true,
                    Content = "{}",
                }
            );

        var request = CreateProvisioningRequest();

        // Act
        await _controller.ProvisionBookingService(request);

        // Assert
        _provisioningService.Verify(
            s => s.CreateBusinessAsync(999, It.IsAny<string>()),
            Times.Once
        );
        _provisioningService.Verify(
            s =>
                s.CreateServiceAsync(
                    "50",
                    999,
                    It.IsAny<BookingProvisioningRequest>(),
                    It.IsAny<string>()
                ),
            Times.Once
        );
    }

    #endregion
}
