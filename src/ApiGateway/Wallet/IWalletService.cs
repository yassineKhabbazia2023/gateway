using ApiGateway.Wallet.Models;

namespace ApiGateway.Wallet;

public interface IWalletService
{
    Task<IEnumerable<WalletResponse>> GetResponsesAsync();
    Task<IEnumerable<AccountPageResponse>> GetAccountsAsync(int i, int page = 1);
}