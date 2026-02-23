using System.Text.Json;
using ApiGateway.Booking.Models;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Booking;

[Route("gtw/booking/api")]
[ApiController]
[Authorize]
public class BookingController(
    IUserContext userContext,
    IBookingExperienceGuards bookingGuards,
    IContactService contactService,
    IBookingProvisioningService bookingProvisioningService,
    ILogger<BookingController> logger) : ControllerBase
{
    [HttpGet("enabled/currentuser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetBookingAccess()
    {
        var userEmail = userContext.User.GetEmail();
        var hasAccess = bookingGuards.IsFeatureFlagEnabled() && bookingGuards.HasAccess(userEmail);

        return Ok(new { hasAccess });
    }

    [HttpPost("booking/provisioning/currentuser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ProvisionBookingService([FromBody] BookingProvisioningRequest request)
    {
        var userEmail = userContext.User.GetEmail();

        if (!bookingGuards.IsFeatureFlagEnabled())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                ErrorCode = Errors.XpBookingFeatureFlagDisabledCode,
                ErrorMessage = Errors.XpBookingFeatureFlagDisabledMessage
            });
        }

        if (!bookingGuards.HasAccess(userEmail))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                ErrorCode = Errors.XpBookingUnauthorizedCode,
                ErrorMessage = Errors.XpBookingUnauthorizedMessage
            });
        }

        var contact = await contactService.GetContactAsync(userEmail);
        if (contact is null)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = Errors.NotFoundContactCode,
                ErrorMessage = string.Format(Errors.NotFoundContactMessage, userEmail)
            });
        }

        var contactId = contact.Id;
        var bearerToken = JwtHelper.ExtractBearerToken(HttpContext.Request);

        if (string.IsNullOrEmpty(bearerToken))
        {
            logger.LogWarning("Bearer token is missing or invalid for ContactId: {ContactId}", contactId);
            return StatusCode(StatusCodes.Status401Unauthorized, new ErrorResponse
            {
                ErrorCode = Errors.MissingBearerTokenCode,
                ErrorMessage = Errors.MissingBearerTokenMessage
            });
        }

        logger.LogInformation("Starting booking provisioning for ContactId: {ContactId}", contactId);

        // Step 1: Create/verify business page
        var businessResponse = await bookingProvisioningService.CreateBusinessAsync(contactId, bearerToken);
        if (!businessResponse.IsSuccessStatusCode)
        {
            logger.LogError("Booking provisioning failed at step 1 (business creation). Status: {StatusCode}", businessResponse.StatusCode);
            return StatusCode((int)businessResponse.StatusCode, new ErrorResponse
            {
                ErrorCode = Errors.BookingProvisioningFailedCode,
                ErrorMessage = string.Format(Errors.BookingProvisioningFailedMessage, businessResponse.Content)
            });
        }

        var businessResult = JsonSerializer.Deserialize<BookingBusinessResponse>(businessResponse.Content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var configurationId = businessResult?.Id ?? 0;

        if (configurationId == 0)
        {
            logger.LogError("Booking provisioning failed: business configuration ID is missing in step 1 response");
            return BadRequest(new ErrorResponse
            {
                ErrorCode = Errors.BookingProvisioningFailedCode,
                ErrorMessage = string.Format(Errors.BookingProvisioningFailedMessage, "Business configuration ID is missing in response")
            });
        }

        logger.LogInformation("Booking business created/verified with ConfigurationId: {ConfigurationId} for ContactId: {ContactId}",
            configurationId, contactId);

        // Step 2: Create service on the business
        var serviceResponse = await bookingProvisioningService.CreateServiceAsync(
            configurationId.ToString(), contactId, request, bearerToken);

        if (!serviceResponse.IsSuccessStatusCode)
        {
            logger.LogError("Booking provisioning failed at step 2 (service creation). Status: {StatusCode}", serviceResponse.StatusCode);
            return StatusCode((int)serviceResponse.StatusCode, new ErrorResponse
            {
                ErrorCode = Errors.BookingProvisioningFailedCode,
                ErrorMessage = string.Format(Errors.BookingProvisioningFailedMessage, serviceResponse.Content)
            });
        }

        logger.LogInformation("Booking provisioning completed successfully for ContactId: {ContactId}, ConfigurationId: {ConfigurationId}",
            contactId, configurationId);

        return StatusCode((int)serviceResponse.StatusCode);
    }
}
