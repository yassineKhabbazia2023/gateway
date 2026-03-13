namespace ApiGateway.FeatureFlags;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string flagKey, string? userEmail = null, CancellationToken cancellationToken = default);
}
