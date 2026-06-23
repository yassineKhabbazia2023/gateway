using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ApiGateway.Identity.Options;
using ApiGateway.Identity.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using ApiGateway.Identity.Repositories;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Handlers;
using System.Diagnostics.CodeAnalysis;
using ApiGateway.Exceptions;
using IdentityModel;

namespace ApiGateway.Identity.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class PulseIdentityServiceAuthenticationExtensions
    {
        public static AuthenticationBuilder AddPulseIdentityServiceAsync(
            this AuthenticationBuilder builder,
            AzureTableAuthorityRepositoryOptions azureTableAuthorityRepositoryOptions,
            IPulseHttpClientFactory httpClientFactory,
            out string[] schemeNames)
        {
            var httpClient = httpClientFactory.CreateClient();

            var authorities = FetchAuthorities(azureTableAuthorityRepositoryOptions);

            schemeNames = authorities.Select(a => a.Name).ToArray();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerPostConfigureOptions>());

            foreach (var authority in authorities)
            {
                builder.RegisterJwtBearerHandler<PulseJwtBearerHandler>(authority, httpClient);
            }

            return builder;

        }

        private static IEnumerable<AuthorityJson> FetchAuthorities(AzureTableAuthorityRepositoryOptions options)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddAzureTableAuthorityRepositoryProvider(opt =>
            {
                opt.IsvcAzureStorageName = options.IsvcAzureStorageName;
                opt.IsvcAzureStorageUri = options.IsvcAzureStorageUri;
                opt.IsvcAzureStorageKey = options.IsvcAzureStorageKey;
                opt.ManagedIdentityClientId = options.ManagedIdentityClientId;
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<IAuthorityRepository>();

            return Task.Run(() => provider.FindAuthorities()).GetAwaiter().GetResult();
        }

        private static AuthenticationBuilder RegisterJwtBearerHandler<T>(
            this AuthenticationBuilder builder,
            AuthorityJson authorityConfig,
            IPulseHttpClientAdapter httpClient) where T : AuthenticationHandler<JwtBearerOptions>
        {
            var signingKeys = FetchSigningKeys(authorityConfig, httpClient);

            builder.AddScheme<JwtBearerOptions, T>(authorityConfig.Name, jwtOptions =>
            {
                ConfigureJwtBearerOptions(jwtOptions, authorityConfig, signingKeys);
            });

            return builder;
        }

        private static List<JsonWebKey> FetchSigningKeys(AuthorityJson authorityConfig, IPulseHttpClientAdapter httpClient)
        {
            var keys = authorityConfig.SigningKeys.Select(k => new JsonWebKey(k.JsonWebKey)).ToList();

            if (!string.IsNullOrWhiteSpace(authorityConfig.JsonWebKeyFetchUrl)
                && Uri.TryCreate(authorityConfig.JsonWebKeyFetchUrl, UriKind.Absolute, out var jsonWebKeyFetchUri))
            {
                var key = httpClient.GetStringAsync(jsonWebKeyFetchUri).GetAwaiter().GetResult();
                keys.Add(new JsonWebKey(key));
            }

            return keys;
        }

        private static void ConfigureJwtBearerOptions(
            JwtBearerOptions options,
            AuthorityJson authorityConfig,
            List<JsonWebKey> signingKeys)
        {
            if (!string.IsNullOrWhiteSpace(authorityConfig.Url))
            {
                options.Authority = authorityConfig.Url;
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuers = authorityConfig.ValidIssuers.Select(i => i.Name),
                ValidAudiences = authorityConfig.ValidAudiences.Select(a => a.Name),
                ValidateAudience = authorityConfig.ValidAudiences.Any(),
                ValidateLifetime = true,
                IssuerSigningKeys = signingKeys,
                NameClaimType = authorityConfig.NameClaimType,
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    await AddRolesToPrincipal(ctx, authorityConfig);
                }
            };
        }

        private static async Task AddRolesToPrincipal(TokenValidatedContext context, AuthorityJson authorityConfig)
        {
            if (context.Principal is null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(context.Principal)));
            }

            foreach (var issuer in authorityConfig.ValidIssuers)
            {
                var claimValue = issuer.CanBeSystemAccount &&
                                 string.IsNullOrWhiteSpace(context.Principal.FindFirst(c => c.Type == ClaimTypes.Email)?.Value)
                    ? "SystemAccount"
                    : issuer.RoleName;

                if (string.IsNullOrWhiteSpace(claimValue))
                {
                    continue;
                }

                var claims = new List<Claim> { new Claim(ClaimTypes.Role, claimValue) };
                var appIdentity = new ClaimsIdentity(claims);

                context.Principal.AddIdentity(appIdentity);
                await Task.CompletedTask;
            }
        }
    }
}
