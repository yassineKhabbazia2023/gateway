namespace ApiGateway.Authorization.Models;

public class AccessRevocationResult
{
    public required string Status { get; set; }
    public required string Message { get; set; }
    public int ContactId { get; set; }
    public int AccountId { get; set; }
    public string? ExternalUserId { get; set; }
    public string? ExternalCompanyId { get; set; }
    public string? Error { get; set; }
}
