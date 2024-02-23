using Kpmg.AspNetCore.Authentication.ConstellationIdentityService;
using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.Extensions;

public static class SecurityServiceExtensions
{
    public static void AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
       
           services
                .AddAuthentication()
                .AddConstellationIdentityService(
                    new ConstellationIdentityServiceAuthenticationOptions
                    {
                        ServerAddress = new Uri(configuration["IdentityServiceApiUrl"] ?? string.Empty),
                        AzureActiveDirectoryClientCredentials =
                        {
                            ClientId =configuration["AADClientId"],
                            ClientSecret = configuration["AADSecret"],
                            Scope = configuration["AADAudience"],
                            Tenant = configuration["AADTenant"],
                        },
                    }, out string[] schemeNames);

            services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddAuthenticationSchemes(schemeNames)
                        .Build();
                });
        
        services.AddConstellationHttpClient();
    }
  
}
