namespace ApiGateway.Pennylane.Models;

public class PennylaneAuthorizationDetails
{
    public required int ContactId { get; set; }
    public required int AccountId { get; set; }
    public string? Role { get; set; }
}
