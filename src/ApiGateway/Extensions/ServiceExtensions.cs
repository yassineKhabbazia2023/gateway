using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text.Json;
using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Security;
using ApiGateway.Wallet;
using Ocelot.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace ApiGateway.Extensions;
[ExcludeFromCodeCoverage]
public static class ServiceExtensions
{
    public static void AddApiGatewayServices(this IServiceCollection services,IConfiguration configuration)
    {
        // Add services to the container.
        services.AddControllers().
            AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy= JsonNamingPolicy.CamelCase);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        //TODO this should use a feature flag in order to disable or enable it  
        ConfigureMockService(services);
        services.AddSwaggerGen(cfg =>
        {
            cfg.DocumentFilter<HideOcelotControllersFilter>();
        });

        services.AddHttpClient<IWalletService, WalletService>(client =>
            {
                client.BaseAddress = new Uri(configuration["BaseUrlOfYourService"] );
            } )
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
            .AddPolicyHandler(GetRetryPolicy());
        
        services.AddOcelot().
            AddDelegatingHandler<AuthenticationHandler>(true)
            .AddDelegatingHandler<AuthorizationHandler>(true)
            .AddDelegatingHandler<MockResponseHandler>(true);
    }

    private static void ConfigureMockService(IServiceCollection services)
    {
        services.AddSingleton<IMockResponseRepository, MockResponseRepository>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IFileSystem, FileSystem>();
    }

    public static void AddJsonConfiguration(this ConfigurationManager configuration)
    {
        // Configuration loading
        configuration.AddJsonFile( BuildOcelotConfigFile(configuration) , optional: false, reloadOnChange: true);
    }

    private static string BuildOcelotConfigFile(ConfigurationManager configuration)
    {
        var mockRepoPath = configuration["OCELOT_CONFIG_PATH"];
        if (string.IsNullOrWhiteSpace(mockRepoPath))
        {
            throw new NullReferenceException("OCELOT_CONFIG_PATH");
        }
        return Path.Combine(mockRepoPath, "ocelot.json");
    }

    private  static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                retryAttempt)));
    }
}
