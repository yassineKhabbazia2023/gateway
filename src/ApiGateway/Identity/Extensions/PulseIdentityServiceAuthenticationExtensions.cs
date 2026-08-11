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
        /// <param name="authorityRepository">
        /// Replacement source for the authorities. Only provided for a local run, where the
        /// Azure table sits behind an unreachable private endpoint.
        /// </param>
        /// <returns>The names of the registered authentication schemes.</returns>
        public static async Task<string[]> AddPulseIdentityServiceAsync(
            this AuthenticationBuilder builder,
            AzureTableAuthorityRepositoryOptions azureTableAuthorityRepositoryOptions,
            IPulseHttpClientFactory httpClientFactory,
            IAuthorityRepository? authorityRepository = null)
        {
            var httpClient = httpClientFactory.CreateClient();

            var authorities = authorityRepository is null
                ? await FetchAuthoritiesAsync(azureTableAuthorityRepositoryOptions)
                : await authorityRepository.FindAuthorities();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerPostConfigureOptions>());

            foreach (var authority in authorities)
            {
                await builder.RegisterJwtBearerHandlerAsync(authority, httpClient);
            }

            return authorities.Select(a => a.Name).ToArray();
        }

        private static async Task<IReadOnlyList<AuthorityJson>> FetchAuthoritiesAsync(AzureTableAuthorityRepositoryOptions options)
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

            return await provider.FindAuthorities();
        }

        private static async Task RegisterJwtBearerHandlerAsync(
            this AuthenticationBuilder builder,
            AuthorityJson authorityConfig,
            IPulseHttpClientAdapter httpClient)
        {
            var signingKeys = await FetchSigningKeysAsync(authorityConfig, httpClient);

            builder.AddScheme<JwtBearerOptions, PulseJwtBearerHandler>(authorityConfig.Name, jwtOptions =>
            {
                ConfigureJwtBearerOptions(jwtOptions, authorityConfig, signingKeys);
            });
        }

        private static async Task<List<JsonWebKey>> FetchSigningKeysAsync(AuthorityJson authorityConfig, IPulseHttpClientAdapter httpClient)
        {
            var keys = authorityConfig.SigningKeys.Select(k => new JsonWebKey(k.JsonWebKey)).ToList();

            if (!string.IsNullOrWhiteSpace(authorityConfig.JsonWebKeyFetchUrl)
                && Uri.TryCreate(authorityConfig.JsonWebKeyFetchUrl, UriKind.Absolute, out var jsonWebKeyFetchUri))
            {
                var key = await httpClient.GetStringAsync(jsonWebKeyFetchUri);
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
