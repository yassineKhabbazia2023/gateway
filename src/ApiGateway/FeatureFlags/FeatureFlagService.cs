using System.Security.Cryptography;
using System.Text;
using OpenFeature;
using OpenFeature.Model;

namespace ApiGateway.FeatureFlags;

public class FeatureFlagService(IFeatureClient featureClient) : IFeatureFlagService
{
    public async Task<bool> IsEnabledAsync(string flagKey, string? userEmail = null, CancellationToken cancellationToken = default)
    {
        var context = BuildEvaluationContext(userEmail);
        return await featureClient.GetBooleanValueAsync(flagKey, false, context, cancellationToken: cancellationToken);
    }

    internal static string HashEmail(string email)
    {
        var normalized = email.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static EvaluationContext? BuildEvaluationContext(string? userEmail)
    {
        if (string.IsNullOrEmpty(userEmail))
            return null;

        var hashedEmail = HashEmail(userEmail);

        return EvaluationContext.Builder()
            .SetTargetingKey(hashedEmail)
            .Build();
    }
}
