using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Options;
using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.Extensions;

public static class SecurityServiceExtensions
{
    public static void AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPulseHttpClientFactory, PulseHttpClientFactory>();
        var httpClient = services.BuildServiceProvider().GetRequiredService<IPulseHttpClientFactory>();
        
        services
             .AddAuthentication()
             .AddPulseIdentityServiceAsync(
                 new AzureTableAuthorityRepositoryOptions { 
                     IsvcAzureStorageName = configuration["IsvcAzureStorageName"],
                     IsvcAzureStorageUri = configuration["IsvcAzureStorageUri"],
                     IsvcAzureStorageKey = configuration["IsvcAzureStorageKey"], 
                     ManagedIdentityClientId = configuration["ManagedIdentityClientId"]
                 },
                 httpClient,
                 out string[] schemeNames);

        services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(schemeNames)
                    .Build();
            });

        services.AddHttpClient();

        services.AddSingleton<IUserContext, AspNetCoreUserContext>();
    }

}
