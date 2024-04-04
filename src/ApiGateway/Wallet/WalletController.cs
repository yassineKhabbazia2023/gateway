using ApiGateway.Wallet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Wallet;

///  Note: this controller is just for a template demo purpose. It should be fixed later 
[ApiController]
[Route("gtw/wallet")]
[Authorize]
public class WalletController(
    IWalletService walletService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WalletResponse>>> Get()
    {
        var responses = await walletService.GetResponsesAsync();
        return Ok(responses);
    }
    /// <summary>
    /// Retrieves a page of accounts.
    /// </summary>
    /// <param name="page">The page number to retrieve, starting from 1.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A paginated list of account responses.</returns>
    /// <response code="200">Returns the paginated list of accounts.</response>
    /// <response code="404">If no accounts are found.</response>
    [HttpGet("page")]
    [ProducesResponseType(typeof(IEnumerable<AccountPageResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<AccountPageResponse>>> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var responses = await walletService.GetAccountsAsync(page,pageSize);
        return Ok(responses);
    }
}