using ApiGateway.ConnectExperience.Services;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Exceptions;

namespace ApiGateway.ConnectExperience.Controller
{
    [Route("gtw/connect/api")]
    [ApiController]
    [Authorize]
    public class ConnectController(IUserContext userContext, IConnectServices experienceServices) : ControllerBase
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
    }
}
