namespace ApiGateway.Pennylane.Models
{
    public class PennylaneAuthorizationRequest
    {
        public int ContactId { get; set; }
        public int AccountId { get; set; }
        public required string Role { get; set; }
    }
}
