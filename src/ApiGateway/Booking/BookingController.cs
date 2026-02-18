using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Booking;

[Route("gtw/booking/api")]
[ApiController]
[Authorize]
public class BookingController(IUserContext userContext, IBookingExperienceGuards bookingGuards) : ControllerBase
{
    [HttpGet("enabled/currentuser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetBookingAccess()
    {
        var userEmail = userContext.User.GetEmail();
        var hasAccess = bookingGuards.IsFeatureFlagEnabled() && bookingGuards.HasAccess(userEmail);

        return Ok(new { hasAccess });
    }
}
