namespace ApiGateway.Authorization.Models;

public class AuthorizationUpdateResponse
{
    public int ContactId { get; set; }
    public int AccountId { get; set; }
    public bool AuthorizationUpdated { get; set; }
    public bool? ProvisioningStepCompleted { get; set; }
    public AccessProvisioningResult? ProvisioningResult { get; set; }
    public bool? RevocationStepCompleted { get; set; }
    public AccessRevocationResult? RevocationResult { get; set; }
}
