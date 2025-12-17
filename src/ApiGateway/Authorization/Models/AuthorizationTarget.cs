namespace ApiGateway.Authorization.Models;

public class AuthorizationTarget
{
    public required int ContactId { get; set; }
    public required int AccountId { get; set; }
    public string? Role { get; set; }
}
