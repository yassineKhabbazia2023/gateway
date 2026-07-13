using System.Security.Cryptography;
using System.Text;
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
        logger.LogDebug("Feature flag '{FlagKey}' evaluated to {Result} for user {UserHash}", flagKey, result, context is not null ? HashEmail(context.Email) : "anonymous");
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

    internal static string HashEmail(string email)
    {
        var normalized = email.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static EvaluationContext? BuildEvaluationContext(FeatureContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var hashedEmail = HashEmail(context.Email);
        return EvaluationContext.Builder()
            .Set("Identifier", hashedEmail)
            .Build();
    }
}
