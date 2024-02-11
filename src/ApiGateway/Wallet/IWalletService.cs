using ApiGateway.Wallet.Models;

namespace ApiGateway.Wallet;

public interface IWalletService
{
    Task<IEnumerable<WalletResponse>> GetResponsesAsync();
}