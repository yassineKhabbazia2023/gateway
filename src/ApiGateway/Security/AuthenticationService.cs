using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Security;

[ExcludeFromCodeCoverage]
//Note:Temporary excluded because it may be we don't need it any more 
public class AuthenticationService(ILogger<AuthenticationService> logger) : IAuthenticationService
{
    
    public bool IsAllowed(HttpRequestMessage request)
    { 
        //TODO Add message if user authentication  succeed or fail    
         logger.LogInformation(" Add message if user authentication  succeed or fail "); 
        return true; 
    }
}