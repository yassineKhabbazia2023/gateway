using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.UserOrigin;

[Route("gtw/userorigin")]
[ApiController]
public class UserOriginController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public UserOriginController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> Index(CancellationToken cancellationToken = default)
    {
        var kpmgIp = _configuration.GetValue<string>("KPMG_IP");

        var callerIp = Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();

        if (kpmgIp.Equals(callerIp))
        {
            return Ok("COLLAB");
        }

        return Ok("CLIENT");
    }
}