using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
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
    /// Downloads the signed SEPA mandate PDF for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>200 with the signed mandate binary stream, or 404 when the prospect or signed mandate is not found.</returns>
    [HttpGet("sepa/content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSignedSepaMandateAsync(
        int prospectId,
        CancellationToken ct)
    {
        var document = await paymentPreferencesService.DownloadSignedSepaMandateAsync(prospectId, ct);
        return document is null
            ? NotFound()
            : File(document.Content, "application/octet-stream", document.FileName);
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
    /// Generates a SEPA mandate and returns the signature URL for the connected signatory.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The multipart SEPA request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>200 with the signature URL, 403 when the connected user is not the prospect signatory, 404 when the prospect is not found, or 502 when Mandat fails.</returns>
    [HttpPost("sepa")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SepaPaymentPreferenceResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SetSepaAsync(
        int prospectId,
        [FromForm] SepaPaymentPreferenceRequest request,
        CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
        {
            ModelState.AddModelError(nameof(request.File), "A non-empty RIB file is required.");
            return ValidationProblem(ModelState);
        }

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

        if (string.IsNullOrWhiteSpace(contact.FirstName) || string.IsNullOrWhiteSpace(contact.LastName))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "InvalidRequest",
                ErrorMessage = "The authenticated contact first name and last name are required."
            });
        }

        var result = await paymentPreferencesService.SetSepaAsync(
            prospectId,
            request,
            contactEmail,
            contact.FirstName,
            contact.LastName,
            contact.Id,
            ct);

        return result.Outcome switch
        {
            SepaPaymentPreferenceOrchestrationOutcome.Completed =>
                Ok(new SepaPaymentPreferenceResponse { SignatureUrl = result.SignatureUrl! }),
            SepaPaymentPreferenceOrchestrationOutcome.Forbidden =>
                Forbid(),
            SepaPaymentPreferenceOrchestrationOutcome.NotFound =>
                NotFound(),
            SepaPaymentPreferenceOrchestrationOutcome.MandateFailed =>
                StatusCode(StatusCodes.Status502BadGateway),
            _ =>
                StatusCode(StatusCodes.Status500InternalServerError)
        };
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
