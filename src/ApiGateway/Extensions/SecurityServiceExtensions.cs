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
                            ClientId =configuration["ConstellationClientId"],
                            ClientSecret = configuration["ConstellationSecret"],
                            Scope = configuration["ConstellationAudience"],
                            Tenant = configuration["ConstellationTenant"],
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
