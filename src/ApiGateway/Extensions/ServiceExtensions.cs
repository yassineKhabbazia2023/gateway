using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Wallet;
using LiteDB;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

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
        AddSwaggerConfig(services, configuration);


        services.AddHttpClient<IWalletService, WalletService>(client =>
            {
                client.BaseAddress = new Uri(configuration["WalletApiBaseUrl"]!);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5)) //Set lifetime to five minutes
            .AddPolicyHandler(GetRetryPolicy());

        services.AddOcelot()
            .AddDelegatingHandler<AuthorizationHandler>(true)
            .AddDelegatingHandler<MockResponseHandler>(true);
    }

    private static void AddSwaggerConfig(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerForOcelot(configuration,
          (o) =>
          {
              o.GenerateDocsDocsForGatewayItSelf(opt =>
              {
                  opt.GatewayDocsTitle = "Gateway";
                  opt.GatewayDocsOpenApiInfo = new()
                  {
                      Title = "Gateway",
                      Version = "v1",
                  };
                  opt.DocumentFilter<HideOcelotControllersFilter>();
                  opt.AddSecurityDefinition("Bearer",
                    new OpenApiSecurityScheme
                    {
                        In = ParameterLocation.Header,
                        Description = "Please enter token",
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        BearerFormat = "JWT",
                        Scheme = "bearer"
                    });
                  opt.AddSecurityRequirement(new OpenApiSecurityRequirement()
                  {
                      {
                          new OpenApiSecurityScheme
                          {
                              Reference = new OpenApiReference
                              {
                                  Type = ReferenceType.SecurityScheme,
                                  Id = "Bearer"
                              },
                              Scheme = "oauth2",
                              Name = "Bearer",
                              In = ParameterLocation.Header,
                          },
                          new List<string>()
                      }
                  });
              });
          },
          config =>
          {
              config.AddServer(new OpenApiServer()
              {
                  Url = "/gateway"
              });
              config.AddServer(new OpenApiServer()
              {
                  Url = "/"
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
        // Configuration loading
        configuration.AddJsonFile(FileHelper.GetOcelotConfigFullPathName(configuration),
            optional: false,
            reloadOnChange: true);
        
    }
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(ConfigConstants.HttpClientRetryAttempt,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                    retryAttempt)));
    }
}