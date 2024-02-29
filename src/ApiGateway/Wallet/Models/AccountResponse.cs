using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Wallet.Models;
[ExcludeFromCodeCoverage]
public class AccountResponse
{
    
    public int AccountId { get; init; }
    public string? CommercialName { get; init; } 
    public string? SourceAccountNumber { get; init; } 
    public string? Source { get; init; } 
    
}


[ExcludeFromCodeCoverage]
public class AccountPageResponse:AccountResponse
{
    public int TotalCount { get; init; }
}


