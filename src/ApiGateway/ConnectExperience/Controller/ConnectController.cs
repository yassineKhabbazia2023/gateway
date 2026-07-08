using ApiGateway.Account;
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
}