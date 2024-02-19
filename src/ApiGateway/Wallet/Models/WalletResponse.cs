using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Wallet.Models;

/// <summary>
/// Note: this just a template it should be deleted once the real implementation was done
/// </summary>
[ExcludeFromCodeCoverage]
public class WalletResponse
{
    public string? SourceNumber { get; set; }
    public DateTime Date { get; set; }
}