using ApiGateway.Account;
using ApiGateway.ConnectExperience.Models;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Pulse.ExceptionMiddleware.Exceptions;
using Pulse.ExceptionMiddleware.Model;
using System.ComponentModel.DataAnnotations;

namespace ApiGateway.ConnectExperience.Controller;

[Route("gtw/connect/api")]
[ApiController]
[Authorize]
public class ConnectController(IUserContext userContext, IConnectServices experienceServices, IAccountService accountService, IContactService contactService) : ControllerBase
{
    [HttpGet("get-user-information")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetUserInformation()
    {
        var userEmail = userContext.User.GetEmail();

        try
        {
            var userInformation = await experienceServices.GetUserInformation(userEmail);
            return Ok(userInformation);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("accounts/{accountId}/summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetSummary([Required(AllowEmptyStrings = false)] int accountId)
    {
        try
        {
            var userEmail = userContext.User.GetEmail();
            var contact = await contactService.GetContactAsync(userEmail) ?? throw new BadRequestException(Errors.NotFoundContactCode, Errors.NotFoundContactMessage);
            var contactId = contact.Id;

            var shouldSkipRoleCheck = await AuthorizationHelper.SkipRoleCheck(accountId, contactId, HttpContext);
            if (!shouldSkipRoleCheck)
            {
                var hasTheNeededRoles = await AuthorizationHelper.CheckRoles(accountId, null, contactId, HttpContext);
                if (!hasTheNeededRoles)
                {
                    return new BadRequestObjectResult(new { ErrorMessage = Errors.NoRoleOnAccountCode, ErrorCode = string.Format(Errors.NoRoleOnAccountMessage, contactId, accountId) });
                }
            }

            var result = await experienceServices.GetSummaryAsync(accountId, contactId, contact.Type!);
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("customers/bulk-invite/{accountId}")]
    public async Task<ActionResult> SendEmailAsync([FromRoute] int accountId, [FromBody] int[] customerIDs, [FromQuery] string? entityType = null)
    {
        var userEmail = userContext.User.GetEmail();
        var result = await experienceServices.SendEmailAsync(userEmail, accountId, customerIDs, entityType);
        return Ok(result);
    }

    /// <summary>
    /// Indique si la modal Sérénité doit être affichée au contact connecté.
    /// </summary>
    /// <remarks>
    /// Décision globale au portefeuille : vraie dès qu'au moins une entité du contact remplit tous les critères.
    /// Aucun accountId n'est attendu, le périmètre est déduit du token.
    /// </remarks>
    /// <returns>Le flag indiquant si la modal doit être affichée.</returns>
    [HttpGet("serenity-modal")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SerenityModalResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SerenityModalResponse>> GetSerenityModal()
    {
        try
        {
            var userEmail = userContext.User.GetEmail();
            var contactId = await ResolveContactIdAsync();
            var shouldDisplay = await experienceServices.GetShouldDisplaySerenityModalAsync(contactId, userEmail);

            return Ok(new SerenityModalResponse(shouldDisplay));
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Enregistre le choix du contact connecté sur la modal Sérénité.
    /// </summary>
    /// <remarks>Le choix est immuable : un second appel retourne 409 Conflict.</remarks>
    /// <param name="request">Le choix exprimé.</param>
    /// <returns>204 No Content si le choix a été enregistré.</returns>
    [HttpPost("serenity-modal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetSerenityModalChoice([FromBody] SerenityChoiceRequest request)
    {
        try
        {
            var userEmail = userContext.User.GetEmail();
            var contactId = await ResolveContactIdAsync();
            await experienceServices.SetSerenityModalChoiceAsync(contactId, userEmail, request.IsAccepted);

            return NoContent();
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (GatewayException ex) when (ex.StatusCode == StatusCodes.Status409Conflict)
        {
            // Traité ici et pas seulement par le middleware : UseExceptionHandler n'est branché
            // qu'hors Development, la recette locale verrait sinon la page d'exception.
            return Conflict(new ErrorResponse { ErrorCode = ex.ErrorCode, ErrorMessage = ex.Message });
        }
    }

    /// <summary>
    /// Résout le contact courant depuis le token. Le contactId ne provient jamais de la route,
    /// du corps ni d'un en-tête entrant : c'est ce qui empêche d'agir pour le compte d'autrui.
    /// </summary>
    private async Task<int> ResolveContactIdAsync()
    {
        var contact = await contactService.GetContactAsync(userContext.User.GetEmail())
            ?? throw new BadRequestException(Errors.NotFoundContactCode, Errors.NotFoundContactMessage);

        return contact.Id;
    }
}