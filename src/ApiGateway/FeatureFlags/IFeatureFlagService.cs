using ApiGateway.FeatureFlags.Models;

namespace ApiGateway.FeatureFlags;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string flagKey, bool defaultValue = false, FeatureContext? context = null, CancellationToken ct = default);
    Task<string> GetStringValueAsync(string flagKey, string defaultValue, FeatureContext? context = null, CancellationToken ct = default);
    Task<int> GetIntValueAsync(string flagKey, int defaultValue, FeatureContext? context = null, CancellationToken ct = default);
}
