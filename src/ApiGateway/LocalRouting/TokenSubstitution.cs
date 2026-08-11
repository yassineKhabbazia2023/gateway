using ApiGateway.Configuration;

namespace ApiGateway.LocalRouting;

/// <summary>
/// Resolves the #{env_id}# and #{env}# tokens that the deployment pipeline normally
/// substitutes. Without this step a '#' remains in a URI authority, which makes the parsing
/// of the SwaggerEndPoints fail.
/// </summary>
public static class TokenSubstitution
{
    public static IDictionary<string, string?> BuildTokenOverrides(IConfiguration merged, LocalRoutingOptions options)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(options);

        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, value) in merged.AsEnumerable())
        {
            if (value is null || !value.Contains("#{", StringComparison.Ordinal))
            {
                continue;
            }

            overrides[key] = value
                .Replace(ConfigConstants.EnvIdToken, options.EnvId, StringComparison.Ordinal)
                .Replace(ConfigConstants.EnvToken, options.Env, StringComparison.Ordinal);
        }

        return overrides;
    }
}
