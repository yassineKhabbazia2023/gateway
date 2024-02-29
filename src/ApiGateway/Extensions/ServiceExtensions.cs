using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Exceptions;
using ApiGateway.Wallet;
using LiteDB;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using static System.Boolean;

namespace ApiGateway.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceExtensions
{
    public static void AddApiGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add services to the container.
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        //TODO this should use a feature flag in order to disable or enable it  
        ConfigureMockService(services,configuration);
        AddSwaggerConfig(services);


        services.AddHttpClient<IWalletService, WalletService>(client =>
            {
                client.BaseAddress = new Uri(configuration["BaseUrlOfYourService"]!);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5)) //Set lifetime to five minutes
            .AddPolicyHandler(GetRetryPolicy());

        services.AddOcelot()
            .AddDelegatingHandler<AuthorizationHandler>(true)
            .AddDelegatingHandler<MockResponseHandler>(true);
    }

    private static void AddSwaggerConfig(IServiceCollection services)
    {
        services.AddSwaggerGen(config =>
        {
            config.DocumentFilter<HideOcelotControllersFilter>();
            config.AddServer(new OpenApiServer()
            {
                Url = "/gateway"
            });
            config.AddServer(new OpenApiServer()
            {
                Url = "/"
            });
            config.AddSecurityDefinition("Bearer",
                new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter token",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "bearer"
                });
            config.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    private static void ConfigureMockService(IServiceCollection services, IConfiguration configuration)
    {

        var databasePath = FileHelper.GetLiteDbDir(configuration);
        services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(databasePath));
        services.AddSingleton<IMockResponseRepository, MockResponseRepository>();
    }

    public static void AddJsonConfiguration(this ConfigurationManager configuration)
    {

        BuildOcelotConfigFile(configuration);
        if (UseLocalOcelotConfig(configuration))
        {
            configuration.AddJsonFile(GetOcelotPath(configuration),
                optional: false,
                reloadOnChange: true);    
        }
        else
        { 
            // Configuration loading
            configuration.AddJsonFile(GetOcelotPath(configuration),
                optional: true,
                reloadOnChange: true);  
        }
        
    }

    private static string GetOcelotPath(IConfiguration configuration)
    {
        return UseLocalOcelotConfig(configuration) ? "configuration/ocelot.json" : TempFileHelper.GetOcelotTempDir();
    }

    private static bool UseLocalOcelotConfig(IConfiguration configuration)
    {
        return TryParse(configuration["USE_LOCAL_OCELOT"],
            out var useLocalOcelotConfig) && useLocalOcelotConfig;
    }

    private static void BuildOcelotConfigFile(ConfigurationManager configuration)
    {
        var ocelotConfig = configuration["OCELOT_CONFIG"];

        if (string.IsNullOrWhiteSpace(ocelotConfig))
        {
            throw new InvalidConfigException( InvalidConfigException.MissingConfigMessage("OCELOT_CONFIG"));
        }
        //due to an issue in how application are deployed (azure web container)

        File.WriteAllText(TempFileHelper.GetOcelotTempDir(),
            ocelotConfig);

    }
        // Configuration loading
        configuration.AddJsonFile(FileHelper.GetOcelotConfigFullPathName(configuration),
            optional: false,
            reloadOnChange: true);
        
    }
    
    //this is a temp fix it should be changed 

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(6,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                    retryAttempt)));
    }
}

