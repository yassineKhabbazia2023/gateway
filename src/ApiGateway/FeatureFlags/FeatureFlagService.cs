using ApiGateway.FeatureFlags.Models;
using OpenFeature;
using OpenFeature.Model;

namespace ApiGateway.FeatureFlags;

public class FeatureFlagService(ILogger<FeatureFlagService> logger) : IFeatureFlagService
{
    private readonly Lazy<FeatureClient> _client = new(() => Api.Instance.GetClient());

    public async Task<bool> IsEnabledAsync(string flagKey, bool defaultValue = false, FeatureContext? context = null, CancellationToken ct = default)
    {
        var evaluationContext = BuildEvaluationContext(context);
        var result = await _client.Value.GetBooleanValueAsync(flagKey, defaultValue, evaluationContext, cancellationToken: ct);
        logger.LogDebug("Feature flag '{FlagKey}' evaluated to {Result} for contact {ContactId}", flagKey, result, context?.ContactId ?? "anonymous");
        return result;
    }

    public async Task<string> GetStringValueAsync(string flagKey, string defaultValue, FeatureContext? context = null, CancellationToken ct = default)
    {
        var evaluationContext = BuildEvaluationContext(context);
        return await _client.Value.GetStringValueAsync(flagKey, defaultValue, evaluationContext, cancellationToken: ct);
    }

    public async Task<int> GetIntValueAsync(string flagKey, int defaultValue, FeatureContext? context = null, CancellationToken ct = default)
    {
        var evaluationContext = BuildEvaluationContext(context);
        return await _client.Value.GetIntegerValueAsync(flagKey, defaultValue, evaluationContext, cancellationToken: ct);
    }

    private static EvaluationContext? BuildEvaluationContext(FeatureContext? context)
    {
        if (context is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(context.ContactId))
        {
            return EvaluationContext.Builder()
                .Set("Identifier", context.ContactId)
                .Build();
        }

        if (string.IsNullOrWhiteSpace(context.Email))
        {
            return null;
        }

        return EvaluationContext.Builder()
            .Set("Identifier", context.Email.ToLowerInvariant())
            .Build();
    }
}
