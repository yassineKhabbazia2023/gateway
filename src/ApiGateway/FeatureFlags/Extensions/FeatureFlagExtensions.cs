using ConfigCat.Client;
using OpenFeature;
using OpenFeature.Contrib.ConfigCat;
using OpenFeature.Providers.Memory;

namespace ApiGateway.FeatureFlags.Extensions;

public static class FeatureFlagExtensions
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        var sdkKey = configuration.GetValue<string>("ConfigCat:SdkKey");

        if (!string.IsNullOrEmpty(sdkKey))
        {
            var provider = new ConfigCatProvider(sdkKey, options =>
            {
                options.PollingMode = PollingModes.AutoPoll(pollInterval: TimeSpan.FromSeconds(60));
                options.DataGovernance = DataGovernance.EuOnly;
            });
            Api.Instance.SetProviderAsync(provider).GetAwaiter().GetResult();
        }
        else
        {
            var flags = BuildInMemoryFlags(configuration);
            var inMemoryProvider = new InMemoryProvider(flags);
            Api.Instance.SetProviderAsync(inMemoryProvider).GetAwaiter().GetResult();
        }

        var featureClient = Api.Instance.GetClient();
        services.AddSingleton<IFeatureClient>(featureClient);
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

        return services;
    }

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
