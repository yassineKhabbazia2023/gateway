using ApiGateway.Wallet.Models;
namespace ApiGateway.Wallet;
public class WalletService : IWalletService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletService> _logger;
    public WalletService(HttpClient httpClient, ILogger<WalletService> logger)
    { 
        //Note: demo purpose to be deleted once is done. Logger cannot be null because it will  be guaranteed by .net 
        Guard.Against.Null(logger);
        _httpClient = httpClient;
        _logger = logger;
    }
    public async Task<IEnumerable<WalletResponse>> GetResponsesAsync()
    {
        // Simulate async operation with Task.FromResult
        return await Task.FromResult(Enumerable.Range(1, 5).Select(index => new WalletResponse
        {
            Date = DateTime.UtcNow,
            SourceNumber = DateTime.UtcNow.Ticks.ToString()
        }).ToArray());
    }
}