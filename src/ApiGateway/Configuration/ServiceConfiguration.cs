using System.Diagnostics.CodeAnalysis;
using ApiGateway.Exceptions;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Http;
using Pulse.ExceptionMiddleware.Exceptions;
namespace ApiGateway.Configuration;

[ExcludeFromCodeCoverage]
public static class ServiceConfiguration
{
    public static void RegisterOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
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
        services.AddOpenTelemetry()
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
                options.SamplingRatio = 1.0f;
            });
    }
}
