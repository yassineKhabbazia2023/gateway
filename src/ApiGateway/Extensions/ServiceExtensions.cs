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
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using ApiGateway.Identity;
using ApiGateway.Identity.Options;
using ApiGateway.Offer;
using ApiGateway.Pennylane;
using ApiGateway.ProspectExperience.Services;
using ApiGateway.ProspectExperience.Validators;
using ApiGateway.Requester;
using ApiGateway.TokenRevocation;
using Azure.Storage.Blobs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ocelot.DependencyInjection;
using Ocelot.Requester;
using Polly;
using Polly.Extensions.Http;
using Pulse.ExceptionMiddleware.Model;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ApiGateway.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceExtensions
{
    /// <summary>Suffix the downstream base URIs must not carry: the client paths own it.</summary>
    private const string ApiPathSegment = "/api";

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
        services.AddScoped<IProspectService, ProspectOrchestrationService>();
        services.AddScoped<IPaymentPreferencesOrchestrationService, PaymentPreferencesOrchestrationService>();
        services.AddScoped<IPaymentPreferenceNotificationService, PaymentPreferenceNotificationService>();
        services.AddScoped<IProspectStepCompletionStrategy, BeneficiaryStepCompletionStrategy>();
        services.AddScoped<IProspectStepCompletionStrategy, SupportingDocumentsStepCompletionStrategy>();
        services.AddScoped<IProspectStepCompletionStrategy, DefaultStepCompletionStrategy>();
        services.AddScoped<IAdditionalSupportingDocumentUploadStrategy, AdditionalSupportingDocumentUploadStrategy>();
        services.AddScoped<ICreatePasswordExperienceService, CreatePasswordExperienceService>();
        services.AddScoped<ICommercialProposalOrchestrationService, CommercialProposalOrchestrationService>();
        services.AddScoped<IEngagementLetterOrchestrationService, EngagementLetterOrchestrationService>();
        services.AddValidatorsFromAssemblyContaining<CreateProspectRequestValidator>();
        services.RegisterOpenTelemetry(configuration);
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ErrorResponse
            {
                ErrorCode = Errors.InvalidRequestCode,
                ErrorMessage = Errors.InvalidRequestMessage
            });
        });
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();

        ConfigureMockService(services, configuration);
        AddSwaggerConfig(services, configuration);

        services.AddHttpClient<IContactService, ContactService>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "ContactApiUri");
        })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IAuthorizationService, AuthorizationService>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "AuthorizationApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IAccountService, AccountService>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "AccountApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IOfferService, OfferService>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "OfferApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        // Add Pennylane HttpClient for company creation
        services.AddHttpClient("PennylaneClient", client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "PennylaneApiUri");
            client.Timeout = TimeSpan.FromSeconds(30); // Add explicit timeout

        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IIdentityService, IdentityService>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "GigyaApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IProspectApiClient, ProspectApiClient>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "ProspectApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IRegistryProspectClient, RegistryProspectClient>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "RegistryApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IMandatePaymentPreferencesClient, MandatePaymentPreferencesClient>(client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "MandateApiUri");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddOcelot()
            .AddDelegatingHandler<TraceContextHandler>(true)
            .AddDelegatingHandler<ContactHandler>(true)
            .AddDelegatingHandler<DownstreamExceptionHandler>(true)
            .AddDelegatingHandler<LogoutRevocationHandler>(true)
            .AddDelegatingHandler<RoleHandler>()
            .AddDelegatingHandler<ExposePrivilegedEndpointsHandler>()
            .AddDelegatingHandler<FeedCenterSettingsHandler>()
            .AddTransientDefinedAggregator<ConfigurationAggregator>()
            .AddTransientDefinedAggregator<PermissionAggregator>()
            .AddTransientDefinedAggregator<SubmissionAggregator>()
            .AddTransientDefinedAggregator<WalletInfoAggregator>()
            .AddDelegatingHandler<BookingSyncHandler>()
            .AddDelegatingHandler<ProspectExperienceHandler>()
            .AddDelegatingHandler<QaSensitiveEndpointsHandler>()
            .AddDelegatingHandler<ApprovedPlatformFilterHandler>()
            .AddDelegatingHandler<FeatureFlagGateHandler>(true)
            .AddDelegatingHandler<MockResponseHandler>(true);

             services.AddSingleton<MessageInvokerHttpRequester>();
             services.Replace(ServiceDescriptor.Singleton<IHttpRequester, WalletInfoProspectFeatureFlagRequester>());


        services.AddHttpClient("BookingClient", client =>
        {
            client.BaseAddress = GetBaseUri(configuration, "BookingApiUri");
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
        // Everything is deferred: with the mocks disabled no storage is required. Building
        // the credential and the URI stays inside the factory, otherwise an empty
        // configuration fails the service registration for a blob that is never used.
        services.AddSingleton(_ => new Lazy<BlobContainerClient>(() =>
        {
            var credential = new Azure.Storage.StorageSharedKeyCredential(
                configuration["IsvcAzureStorageName"]!,
                configuration["IsvcAzureStorageKey"]!);
            var containerUri = new Uri($"{configuration["IsvcAzureBlobStorageUri"]!.TrimEnd('/')}/{MocksConstants.ContainerName}");

            var client = new BlobContainerClient(containerUri, credential);
            client.CreateIfNotExists();
            return client;
        }));
        services.AddSingleton<IMockResponseRepository, MockResponseRepository>();
    }

    public static void AddJsonConfiguration(this ConfigurationManager configuration)
    {
        // Configuration loading
        configuration.AddJsonFile(FileHelper.GetOcelotConfigFullPathName(configuration),
            optional: false,
            reloadOnChange: true);
    }

    /// <summary>
    /// Reads a downstream base URI and normalises it to the root of the service, with a
    /// trailing slash.
    ///
    /// The trailing slash is one half of the contract: without it <see cref="HttpClient"/>
    /// drops the last segment of the base path when combining it with a relative request URI,
    /// so a base URL carrying a path prefix (a reverse proxy route such as /offer) would
    /// silently lose it.
    ///
    /// Dropping a trailing /api segment is the other half. The clients used to address their
    /// downstream with a root relative path (/api/subscription), which makes HttpClient
    /// discard the base path entirely: whatever the configured value carried was dead weight,
    /// and several environments do carry an /api suffix. Now that every client path is
    /// relative and starts with api/, keeping that suffix would produce /api/api. Normalising
    /// here keeps those deployed values working instead of requiring a settings change in
    /// every environment.
    /// </summary>
    internal static Uri GetBaseUri(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new GatewayException(StatusCodes.Status500InternalServerError, Errors.NullConfigurationCode, string.Format(Errors.NullConfigurationMessage, key));
        }

        // Parsed rather than trimmed as a string: a host such as api-itg01.itg.pulse.rydge.fr
        // must never be mistaken for the segment being stripped.
        var uri = new Uri(value, UriKind.Absolute);
        var path = uri.AbsolutePath.TrimEnd('/');

        if (path.EndsWith(ApiPathSegment, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^ApiPathSegment.Length];
        }

        return new UriBuilder(uri) { Path = $"{path}/" }.Uri;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(ConfigConstants.HttpClientRetryAttempt,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                    retryAttempt)));
    }

    private static IServiceCollection AddBookingExperienceConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<XpBookingOptions>()
            .Configure(opt =>
            {
                opt.FeatureFlagEnabled = configuration.GetValue<bool?>("XpBookingFeatureFlagEnabled") ?? false;
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
                opt.GigyaApiKey = configuration[ConfigConstants.GigyaApiKeyConfigKey]!;
                opt.GigyaSecret = configuration["GigyaSecret"]!;
                opt.GigyaUserKey = configuration["GigyaUserKey"]!;
                opt.CollaboratorsSecurityGroup = configuration["CollaboratorsSecurityGroup"]!;
                opt.AdministratorsSecurityGroup = configuration["AdministratorsSecurityGroup"];
            }
        });
        return services;
    }
}
