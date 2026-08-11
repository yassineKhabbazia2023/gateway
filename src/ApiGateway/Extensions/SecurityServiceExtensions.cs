using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Options;
using ApiGateway.Identity.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.Extensions;

public static class SecurityServiceExtensions
{
    public static async Task AddAuthenticationServicesAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddSingleton<IPulseHttpClientFactory, PulseHttpClientFactory>();
        var httpClient = services.BuildServiceProvider().GetRequiredService<IPulseHttpClientFactory>();

        // On a workstation the Azure table of the authorities sits behind a private
        // endpoint: the JWT schemes are then described in appsettings.Development.json.
        IAuthorityRepository? localAuthorities = environment.IsDevelopment()
            ? new ConfigurationAuthorityRepository(configuration)
            : null;

        var schemeNames = await services
             .AddAuthentication()
             .AddPulseIdentityServiceAsync(
                 new AzureTableAuthorityRepositoryOptions {
                     IsvcAzureStorageName = configuration["IsvcAzureStorageName"],
                     IsvcAzureStorageUri = configuration["IsvcAzureStorageUri"],
                     IsvcAzureStorageKey = configuration["IsvcAzureStorageKey"]
                 },
                 httpClient,
                 localAuthorities);

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
