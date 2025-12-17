namespace ApiGateway.Authorization.Models;

public class AuthorizationUpdateRequest
{
    public required IList<string> PermissionsCodes { get; set; }

    public required AuthorizationTarget Authorization { get; set; }
}
