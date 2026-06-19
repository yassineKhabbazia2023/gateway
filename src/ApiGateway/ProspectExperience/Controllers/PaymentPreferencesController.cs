using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.ProspectExperience.Controllers;

/// <summary>
/// Exposes Gateway payment preference endpoints for prospect onboarding.
/// </summary>
[ApiController]
[Authorize]
[Route("gtw/prospect/api/onboarding/{prospectId}/payment-preferences")]
public sealed class PaymentPreferencesController(
    IUserContext userContext,
    IContactService contactService,
    IPaymentPreferencesOrchestrationService paymentPreferencesService) : ControllerBase
{
    /// <summary>
    /// Gets the current payment preference.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The current payment preference, or 404 when the prospect is not found.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentPreferenceResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentPreferenceResponse>> GetAsync(
        int prospectId,
        CancellationToken ct)
    {
        var response = await paymentPreferencesService.GetAsync(prospectId, ct);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Sets the current payment preference to OTHER.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>204 when completed, 400 when the authenticated email is unavailable, or 404 when the prospect is not found.</returns>
    [HttpPost("other")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetOtherAsync(
        int prospectId,
        CancellationToken ct)
    {
        var contactEmail = userContext.User.GetEmail();
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "InvalidRequest",
                ErrorMessage = "The authenticated user email is required."
            });
        }

        var contact = await contactService.GetContactAsync(contactEmail);
        if (contact is null)
        {
            return NotFound();
        }

        var saved = await paymentPreferencesService.SetOtherAsync(prospectId, contactEmail, contact.Id, ct);
        return saved ? NoContent() : NotFound();
    }

    /// <summary>
    /// Resets the current payment preference and the payment method onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>204 when reset, or 404 when the prospect or payment preference is not found.</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetAsync(
        int prospectId,
        CancellationToken ct)
    {
        var reset = await paymentPreferencesService.ResetAsync(prospectId, ct);
        return reset ? NoContent() : NotFound();
    }
}
