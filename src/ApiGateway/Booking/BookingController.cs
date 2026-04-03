using ApiGateway.Contact;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Booking;

[Route("gtw/booking/api")]
[ApiController]
[Authorize]
public class BookingController(
    IUserContext userContext,
    IBookingExperienceGuards bookingGuards,
    IContactService contactService,
    IBookingSyncTrigger syncTrigger,
    ILogger<BookingController> logger) : ControllerBase
{
    [HttpGet("enabled/currentuser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetBookingAccess()
    {
        var userEmail = userContext.User.GetEmail();
        var hasAccess = bookingGuards.IsFeatureFlagEnabled() && bookingGuards.HasAccess(userEmail);

        if (hasAccess)
        {
            try
            {
                var contact = await contactService.GetContactAsync(userEmail);
                if (contact is not null && syncTrigger.ShouldTriggerSync(contact.Id))
                {
                    syncTrigger.TriggerSync(contact.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve contact for sync trigger.");
            }
        }

        return Ok(new { hasAccess });
    }
}
