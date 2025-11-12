using System.Diagnostics.CodeAnalysis;
using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Trace;
using Pulse.ExceptionMiddleware.Exceptions;
namespace ApiGateway.Configuration;

[ExcludeFromCodeCoverage]
public static class ServiceConfiguration
{
    public static void RegisterApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new GatewayException(StatusCodes.Status406NotAcceptable,Errors.NullArgumentCode, string.Format(Errors.NullConfigurationMessage, nameof(configuration)));
        }
        var applicationInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrEmpty(applicationInsightsConnectionString))
        {
            throw new GatewayException(StatusCodes.Status406NotAcceptable, Errors.NullArgumentCode, string.Format(Errors.NullConfigurationMessage, nameof(applicationInsightsConnectionString)));
        }
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
            // Désactiver explicitement tous les types de sampling
            options.EnableAdaptiveSampling = false;
        });
    }
}
