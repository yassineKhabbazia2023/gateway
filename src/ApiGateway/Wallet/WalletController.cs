using ApiGateway.Wallet.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Wallet;
///  Note: this controller is just for a template demo purpose. It should be fixed later 
[ApiController]
[Route("api/wallet")]
public class WalletController(IWalletService walletService, ILogger<WalletController> logger)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IEnumerable<WalletResponse>>> Get()
    {
        var responses = await walletService.GetResponsesAsync();
        return Ok(responses);
    }
}