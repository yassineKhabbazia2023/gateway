using ApiGateway.Exceptions;
using ApiGateway.Identity.Adapters;
using ApiGateway.Identity.Factories;
using ApiGateway.Identity.Options;
using ApiGateway.Identity.Repositories;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Extensions
{
    /// <summary>
    /// Contains extensions methods to register client components of the Identity Service for the <see cref="ServiceCollection"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class IdentityServiceClientServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureTableAuthorityRepositoryProvider(this IServiceCollection services, Action<AzureTableAuthorityRepositoryOptions> options)
        {
            if (services == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullConfigurationCode, string.Format(Errors.NullConfigurationMessage, nameof(services)));
            }

            if (options == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullConfigurationCode, string.Format(Errors.NullConfigurationMessage, nameof(options)));
            }
            services.AddScoped<ITableClientFactory, TableClientFactory>();
            services.AddScoped<ITableClientAdapter, TableClientAdapter>();
            services.AddSingleton<IAuthorityRepository, AzureTableAuthorityRepository>();
            services.AddOptions<AzureTableAuthorityRepositoryOptions>().Configure(options);

            return services;
        }
    }
}
