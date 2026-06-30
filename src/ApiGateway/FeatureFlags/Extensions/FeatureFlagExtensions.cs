using ApiGateway.FeatureFlags.Options;
using ConfigCat.Client;
using OpenFeature;
using OpenFeature.Contrib.ConfigCat;
using OpenFeature.Providers.Memory;

namespace ApiGateway.FeatureFlags.Extensions;

public static class FeatureFlagExtensions
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeatureFlagOptions>(configuration.GetSection(FeatureFlagOptions.SectionName));
        services.Configure<ConfigCatSettings>(configuration.GetSection("ConfigCat"));
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
        return services;
    }

    public static async Task UseFeatureFlagsAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FeatureFlagExtensions));
        var configCat = app.Configuration.GetSection("ConfigCat").Get<ConfigCatSettings>() ?? new ConfigCatSettings();

        if (!string.IsNullOrWhiteSpace(configCat.SdkKey))
        {
            var provider = new ConfigCatProvider(configCat.SdkKey, configCatOptions =>
            {
                configCatOptions.BaseUrl = new Uri(configCat.BaseUrl);
                configCatOptions.PollingMode = PollingModes.AutoPoll(
                    pollInterval: TimeSpan.FromSeconds(configCat.PollIntervalSeconds));
            });
            await Api.Instance.SetProviderAsync(provider);
            logger.LogInformation("Feature flags: ConfigCat provider configured (BaseUrl: {BaseUrl}, PollInterval: {PollInterval}s)",
                configCat.BaseUrl, configCat.PollIntervalSeconds);
        }
        else
        {
            logger.LogWarning("Feature flags: ConfigCat SdkKey is empty — falling back to InMemory provider with default values");
            var flags = BuildInMemoryFlags(app.Configuration);
            await Api.Instance.SetProviderAsync(new InMemoryProvider(flags));
        }
    }

    // Note: The InMemory fallback only supports boolean flags.
    // String and integer flags from FeatureFlags:Defaults are ignored.
    private static IDictionary<string, Flag> BuildInMemoryFlags(IConfiguration configuration)
    {
        var flags = new Dictionary<string, Flag>();
        var section = configuration.GetSection("FeatureFlags:Defaults");

        foreach (var child in section.GetChildren())
        {
            if (bool.TryParse(child.Value, out var boolValue))
            {
                flags[child.Key] = new Flag<bool>(
                    new Dictionary<string, bool> { { "on", true }, { "off", false } },
                    boolValue ? "on" : "off");
            }
        }

        return flags;
    }
}
