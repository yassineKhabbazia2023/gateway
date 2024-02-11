namespace ApiGateway.Security;

public class AuthenticationService(ILogger<AuthenticationService> logger) : IAuthenticationService
{
    public bool IsAllowed(HttpRequestMessage request)
    { 
        //TODO Add message if user authentication  succeed or fail    
         //logger.LogInformation(""); 
        return true; 
    }
}