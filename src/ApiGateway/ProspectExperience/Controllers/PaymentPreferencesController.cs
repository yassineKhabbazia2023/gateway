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
[Route("gtw/prospect/api/onboarding/{accountId}/payment-preferences")]
public sealed class PaymentPreferencesController(
    IUserContext userContext,
    IContactService contactService,
    IProspectApiClient prospectApiClient,
    IPaymentPreferencesOrchestrationService paymentPreferencesService) : ControllerBase
{
    /// <summary>
    /// Gets the current payment preference.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The current payment preference.</returns>
    /// <response code="200">Returns the current payment preference.</response>
    /// <response code="404">The prospect is not found.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentPreferenceResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentPreferenceResponse>> GetAsync(
        int accountId,
        CancellationToken ct)
    {
        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            return NotFound();
        }

        var contactEmail = userContext.User.GetEmail();
        var response = await paymentPreferencesService.GetAsync(prospectId.Value, contactEmail, ct);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Downloads the signed SEPA mandate PDF for a prospect.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The signed SEPA mandate binary stream.</returns>
    /// <response code="200">Returns the signed SEPA mandate as a binary stream.</response>
    /// <response code="404">The prospect or the signed mandate is not found.</response>
    [HttpGet("sepa/content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSignedSepaMandateAsync(
        int accountId,
        CancellationToken ct)
    {
        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            return NotFound();
        }

        var document = await paymentPreferencesService.DownloadSignedSepaMandateAsync(prospectId.Value, ct);
        return document is null
            ? NotFound()
            : File(
                document.Content,
                string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType,
                document.FileName);
    }

    /// <summary>
    /// Sets the current payment preference to OTHER.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>No content when the payment preference is set to OTHER.</returns>
    /// <response code="204">The payment preference was set to OTHER.</response>
    /// <response code="400">The authenticated user email is unavailable.</response>
    /// <response code="404">The prospect or the contact is not found.</response>
    [HttpPost("other")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetOtherAsync(
        int accountId,
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

        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            return NotFound();
        }

        var contact = await contactService.GetContactAsync(contactEmail);
        if (contact is null)
        {
            return NotFound();
        }

        var saved = await paymentPreferencesService.SetOtherAsync(prospectId.Value, contactEmail, contact.Id, ct);
        return saved ? NoContent() : NotFound();
    }

    /// <summary>
    /// Generates a SEPA mandate and returns the signature URL for the connected signatory.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="request">The multipart SEPA request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The SEPA mandate signature URL for the connected signatory.</returns>
    /// <response code="200">Returns the signature URL for the connected signatory.</response>
    /// <response code="400">The request is invalid (missing RIB file, authenticated email, or contact name).</response>
    /// <response code="403">The connected user is not the prospect signatory.</response>
    /// <response code="404">The prospect or the contact is not found.</response>
    /// <response code="502">The Mandat service failed to generate the mandate.</response>
    [HttpPost("sepa")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SepaPaymentPreferenceResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SetSepaAsync(
        int accountId,
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

        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            return NotFound();
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
            prospectId.Value,
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
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>No content when the payment preference and onboarding step are reset.</returns>
    /// <response code="204">The payment preference and onboarding step were reset.</response>
    /// <response code="404">The prospect or the payment preference is not found.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetAsync(
        int accountId,
        CancellationToken ct)
    {
        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            return NotFound();
        }

        var reset = await paymentPreferencesService.ResetAsync(prospectId.Value, ct);
        return reset ? NoContent() : NotFound();
    }
}
