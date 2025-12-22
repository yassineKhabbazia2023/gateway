using ApiGateway.Pennylane.Models;

namespace ApiGateway.Authorization.Models;

public class AuthorizationUpdateRequest
{
    public required IList<string> PermissionsCodes { get; set; }

    public required PennylaneAuthorizationDetails Authorization { get; set; }
}
