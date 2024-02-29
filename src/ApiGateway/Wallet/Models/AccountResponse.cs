namespace ApiGateway.Wallet.Models;

public class AccountResponse
{
    
    public int AccountId { get; init; }
    public string? CommercialName { get; init; } 
    public string? SourceAccountNumber { get; init; } 
    public string? Source { get; init; } 
    
}


public class AccountPageResponse:AccountResponse
{
    public int TotalCount { get; init; }
}


