using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Account;
using ApiGateway.Aggregrator;
using ApiGateway.Authorization;
using ApiGateway.Cache;
using ApiGateway.Configuration;
using ApiGateway.Contact;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Helpers;
using ApiGateway.Identity;
using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Options;
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
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAuthorizationSevice, AuthorizationSevice>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IIdentityService,IdentityService>();
        services.RegisterApplicationInsights(configuration);
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();

        ConfigureMockService(services,configuration);
        AddSwaggerConfig(services, configuration);

        services.AddHttpClient<IContactService, ContactService>(client =>
            {
                client.BaseAddress = new Uri(configuration["ContactApiUri"]!);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IAuthorizationSevice, AuthorizationSevice>(client =>
        {
            client.BaseAddress = new Uri(configuration["AuthorizationApiUri"]!);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IAccountService, AccountService>(client =>
        {
            client.BaseAddress = new Uri(configuration["AccountApiUri"]!);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IIdentityService, IdentityService>(client =>
        {
            client.BaseAddress = new Uri(configuration["GigyaApiUri"]!);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddOcelot()
            .AddDelegatingHandler<ContactHandler>(true)
            .AddTransientDefinedAggregator<ConfigurationAggregator>()
            .AddTransientDefinedAggregator<PermissionAggregator>()
            .AddDelegatingHandler<MockResponseHandler>(true);

        services.AddGigyaConfiguration(configuration);
    }

    private static void AddSwaggerConfig(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerForOcelot(configuration,
          (o) =>
          {
              o.GenerateDocsDocsForGatewayItSelf(opt =>
              {
                  opt.GatewayDocsTitle = "API Gateway Desktop";
                  opt.GatewayDocsOpenApiInfo = new()
                  {
                      Title = "API Gateway Desktop",
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
                  Url = "/desktop"
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

    private static void RegisterApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        var applicationInsightsConexionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = applicationInsightsConexionString;
        });
    }

    private static IServiceCollection AddGigyaConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentityServiceOptions>(opt =>
        {
            if (configuration is not null)
            {
                opt.GigyaApiKey = configuration["GigyaApiKey"]!;
                opt.GigyaSecret = configuration["GigyaSecret"]!;
                opt.GigyaUserKey = configuration["GigyaUserKey"]!;
                opt.CollaboratorsSecurityGroup = configuration["CollaboratorsSecurityGroup"]!;

            }
        });
        return services;
    }
}