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
    public ActionResult<string> Index(CancellationToken cancellationToken = default)
    {
        var rydgeIp = _configuration.GetValue<string>("RYDGE_IP");

        var callerIp = Request.Headers["X-REAL-IP"];

        if (!string.IsNullOrEmpty(rydgeIp) && rydgeIp == callerIp)
        {
            return Ok("COLLAB");
        }

        return Ok("CLIENT");
    }
}
