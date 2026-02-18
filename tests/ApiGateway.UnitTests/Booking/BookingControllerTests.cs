using ApiGateway.Booking;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Booking;

public class BookingControllerTests
{
    private static Mock<IUserContext> CreateUserContext(string email)
    {
        var claim = new Claim(ClaimValueTypes.Email, email);
        var userClaimPrincipal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(userClaimPrincipal.Object);
        userContext.Setup(f => f.User.FindFirst(It.IsAny<string>())).Returns(claim);
        return userContext;
    }

    [Fact]
    public void GetBookingAccess_FeatureFlagEnabled_UserHasAccess_ReturnsHasAccessTrue()
    {
        // Arrange
        var userContext = CreateUserContext("user@test.fr");
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        bookingGuards.Setup(s => s.HasAccess("user@test.fr")).Returns(true);
        var controller = new BookingController(userContext.Object, bookingGuards.Object);

        // Act
        var result = controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = true });
    }

    [Fact]
    public void GetBookingAccess_FeatureFlagDisabled_UserHasAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        var userContext = CreateUserContext("user@test.fr");
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        bookingGuards.Setup(s => s.HasAccess("user@test.fr")).Returns(true);
        var controller = new BookingController(userContext.Object, bookingGuards.Object);

        // Act
        var result = controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }

    [Fact]
    public void GetBookingAccess_FeatureFlagEnabled_UserHasNoAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        var userContext = CreateUserContext("notlisted@test.fr");
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(true);
        bookingGuards.Setup(s => s.HasAccess("notlisted@test.fr")).Returns(false);
        var controller = new BookingController(userContext.Object, bookingGuards.Object);

        // Act
        var result = controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }

    [Fact]
    public void GetBookingAccess_FeatureFlagDisabled_UserHasNoAccess_ReturnsHasAccessFalse()
    {
        // Arrange
        var userContext = CreateUserContext("notlisted@test.fr");
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.IsFeatureFlagEnabled()).Returns(false);
        bookingGuards.Setup(s => s.HasAccess("notlisted@test.fr")).Returns(false);
        var controller = new BookingController(userContext.Object, bookingGuards.Object);

        // Act
        var result = controller.GetBookingAccess() as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Value.Should().BeEquivalentTo(new { hasAccess = false });
    }
}
