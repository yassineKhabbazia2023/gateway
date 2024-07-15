using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Configuration;

[ExcludeFromCodeCoverage]
public static class ServiceConfiguration
{
    public static void RegisterApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var applicationInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        ArgumentNullException.ThrowIfNullOrEmpty(applicationInsightsConnectionString);
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
        });
    }
}
