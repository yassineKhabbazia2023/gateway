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

        var databasePath = TempFileHelper.GetLiteDbTempDir();
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(databasePath));
        services.AddSingleton<IMockResponseRepository, MockResponseRepository>();
    }

    public static void AddJsonConfiguration(this ConfigurationManager configuration)
    {
        BuildOcelotConfigFile(configuration);
        // Configuration loading
        configuration.AddJsonFile(TempFileHelper.GetOcelotTempDir()  , optional: false, reloadOnChange: true);
    }

    private static void BuildOcelotConfigFile(ConfigurationManager configuration)
    {
        var ocelotConfig = configuration["OCELOT_CONFIG"];
          
        if (string.IsNullOrWhiteSpace(ocelotConfig))
        {
            throw new NullReferenceException("OCELOT_CONFIG");
        }
        //due to an issue in how application are deployed (azure web container)
        
        File.WriteAllText(TempFileHelper.GetOcelotTempDir(), ocelotConfig);
        
    }
    //this is a temp fix it should be changed 

    private  static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                retryAttempt)));
    }
}
