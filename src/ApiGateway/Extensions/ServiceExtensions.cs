using ApiGateway.Account;
using ApiGateway.Aggregator;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Validators;
using ApiGateway.Booking;
using ApiGateway.Booking.Options;
using ApiGateway.Cache;
using ApiGateway.Configuration;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Contact;
using ApiGateway.DelegatingHandlers;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Helpers;
using ApiGateway.Identity;
using ApiGateway.Identity.Options;
using ApiGateway.Offer;
using ApiGateway.Pennylane;
using ApiGateway.TokenRevocation;
using Azure.Storage.Blobs;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ocelot.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ApiGateway.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceExtensions
{
    public static void AddApiGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add services to the container.
        services.AddMemoryCache();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IConnectServices, ConnectServices>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPermissionValidationService, PermissionValidationService>();
        services.AddScoped<IPennylaneService, PennylaneService>();

        // Token Revocation Cache
        services.AddSingleton<ITokenRevocationCache, TokenRevocationCache>();

        services.AddScoped<IAuthorizationRequestValidator, AuthorizationRequestValidator>();
        services.AddScoped<IPennylaneAuthorizationService, PennylaneAuthorizationService>();
        services.AddScoped<IAuthorizationWorkflowService, AuthorizationWorkflowService>();
        services.RegisterApplicationInsights(configuration);
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();

        ConfigureMockService(services, configuration);
        AddSwaggerConfig(services, configuration);

        services.AddHttpClient<IContactService, ContactService>(client =>
        {
            client.BaseAddress = new Uri(configuration["ContactApiUri"]!);
        })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IAuthorizationService, AuthorizationService>(client =>
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

        services.AddHttpClient<IOfferService, OfferService>(client =>
        {
            client.BaseAddress = new Uri(configuration["OfferApiUri"]!);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        // Add Pennylane HttpClient for company creation
        services.AddHttpClient("PennylaneClient", client =>
        {
            client.BaseAddress = new Uri(configuration["PennylaneApiUri"]!);
            client.Timeout = TimeSpan.FromSeconds(30); // Add explicit timeout

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
            .AddDelegatingHandler<DownstreamExceptionHandler>(true)
            .AddDelegatingHandler<LogoutRevocationHandler>(true)
            .AddDelegatingHandler<RoleHandler>()
            .AddDelegatingHandler<ExposePrivilegedEndpointsHandler>()
            .AddDelegatingHandler<FeedCenterSettingsHandler>()
            .AddTransientDefinedAggregator<ConfigurationAggregator>()
            .AddTransientDefinedAggregator<PermissionAggregator>()
            .AddTransientDefinedAggregator<SubmissionAggregator>()
            .AddDelegatingHandler<BookingWhitelistHandler>()
            .AddDelegatingHandler<BookingFeatureFlagHandler>()
            .AddDelegatingHandler<BookingSyncHandler>()
            .AddDelegatingHandler<ProspectExperienceHandler>()
            .AddDelegatingHandler<MockResponseHandler>(true);

        services.AddHttpClient("BookingClient", client =>
        {
            client.BaseAddress = new Uri(configuration["BookingApiUri"]!);
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<IBookingSyncTrigger, BookingSyncTrigger>();
        services.AddSingleton<IBookingExperienceGuards, BookingExperienceGuards>();
        services.AddGigyaConfiguration(configuration);
        services.AddBookingExperienceConfiguration(configuration);
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
        var credential = new Azure.Storage.StorageSharedKeyCredential(
            configuration["IsvcAzureStorageName"]!,
            configuration["IsvcAzureStorageKey"]!);
        var containerUri = new Uri($"{configuration["IsvcAzureBlobStorageUri"]!.TrimEnd('/')}/{MocksConstants.ContainerName}");
        services.AddSingleton(_ =>
        {
            var client = new BlobContainerClient(containerUri, credential);
            client.CreateIfNotExists();
            return client;
        });
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
            // Désactiver explicitement tous les types de sampling
            options.EnableAdaptiveSampling = false;
        });
    }

    private static IServiceCollection AddBookingExperienceConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<XpBookingOptions>()
            .Configure(opt =>
            {
                opt.FeatureFlagEnabled = configuration.GetValue<bool?>("XpBookingFeatureFlagEnabled") ?? false;
                opt.Whitelist = configuration["XpBookingWhitelist"] ?? string.Empty;
                opt.SyncIntervalMinutes = configuration.GetValue<int?>("XpBookingSyncIntervalMinutes") ?? 5;
            });
        return services;
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
                opt.AdministratorsSecurityGroup = configuration["AdministratorsSecurityGroup"];
            }
        });
        return services;
    }
}
