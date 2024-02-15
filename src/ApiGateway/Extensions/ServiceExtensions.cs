using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Security;
using ApiGateway.Wallet;
using LiteDB;
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
                client.BaseAddress = new Uri(configuration["BaseUrlOfYourService"]! );
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
        
        var databasePath = $"Filename={MocksConstants.FileDbName}";
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(databasePath));
        services.AddSingleton<IMockResponseRepository, MockResponseRepository>();
    }

    public static void AddJsonConfiguration(this ConfigurationManager configuration)
    {
        // Configuration loading
        configuration.AddJsonFile( BuildOcelotConfigFile(configuration) , optional: false, reloadOnChange: true);
    }

    private static string BuildOcelotConfigFile(ConfigurationManager configuration)
    {
        var ocelotConfig = configuration["OCELOT_CONFIG"];
        if (string.IsNullOrWhiteSpace(ocelotConfig))
        {
            throw new NullReferenceException("OCELOT_CONFIG");
        }
        
        File.WriteAllText(ConfigConstants.OcelotConfigFile, ocelotConfig);
        return ConfigConstants.OcelotConfigFile;
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
