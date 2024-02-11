namespace ApiGateway.Security;

public interface IAuthenticationService
{
    bool IsAllowed(HttpRequestMessage request);
}