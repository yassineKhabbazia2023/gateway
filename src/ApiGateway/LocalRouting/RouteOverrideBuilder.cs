using System.Globalization;
using System.Text.RegularExpressions;
using ApiGateway.Configuration;

namespace ApiGateway.LocalRouting;

/// <summary>Rewriting result: the overrides to inject, and the declared services not found.</summary>
public sealed record RouteOverrideResult(
    IDictionary<string, string?> Overrides,
    IReadOnlyList<string> UnknownServices);

/// <summary>
/// Rewrites the downstream of every route for a run on a development workstation.
/// Three cases, in that order:
/// <list type="number">
/// <item>service declared in <see cref="LocalRoutingOptions.Services"/>: it runs on the workstation;</item>
/// <item>service having a public prefix: reached directly on the public entry point, with the
/// prefix in front of its original path;</item>
/// <item>otherwise: chained through the deployed gateway.</item>
/// </list>
///
/// Case 2 is not a convenience. The delegating handlers translate the path meant for the
/// microservice — <c>ContactHandler</c> replaces the <c>currentuser</c> segment with a
/// <c>contactId</c> parameter, and two other fragments meet the same fate. That translation
/// only makes sense against the microservice itself: chained to the deployed gateway, it
/// produces a URL that none of its upstream routes recognizes, hence a 404. Chaining is
/// therefore only usable for the services without a public prefix, whose routes never carry
/// those fragments.
///
/// The Aggregates section is left untouched: it delegates to its RouteKeys, which are rewritten.
/// </summary>
public static class RouteOverrideBuilder
{
    /// <summary>The pattern is short and anchored; the bound only ensures the startup never blocks.</summary>
    private static readonly TimeSpan HostPatternTimeout = TimeSpan.FromSeconds(1);

    public static RouteOverrideResult BuildRouteOverrides(IConfiguration merged, LocalRoutingOptions options)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(options);

        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);
        var publicRoot = new Uri(options.PublicBaseUrl);
        var matchedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hostPattern = BuildHostPattern(options.EnvId);

        foreach (var route in merged.GetSection(ConfigConstants.RoutesSection).GetChildren())
        {
            var upstream = route[ConfigConstants.UpstreamPathTemplateKey];
            var service = ExtractService(upstream);

            if (service is null)
            {
                continue;
            }

            matchedServices.Add(service);
            var preserveScheme = ConfigConstants.WebSocketScheme.Equals(
                route[ConfigConstants.DownstreamSchemeKey],
                StringComparison.OrdinalIgnoreCase);

            if (options.Services.TryGetValue(service, out var localBaseUrl))
            {
                ApplyTarget(overrides, route.Path, new DownstreamTarget(new Uri(localBaseUrl), Path: null, preserveScheme));
                continue;
            }

            var downstreamPath = ResolveDownstreamPath(route, upstream!, options, hostPattern);
            ApplyTarget(overrides, route.Path, new DownstreamTarget(publicRoot, downstreamPath, preserveScheme));
        }

        overrides[ConfigConstants.GlobalBaseUrlKey] = options.BaseUrl;

        var unknown = options.Services.Keys
            .Where(service => !matchedServices.Contains(service))
            .OrderBy(service => service, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RouteOverrideResult(overrides, unknown);
    }

    /// <summary>
    /// Public prefix if the service has one — the downstream then stays the path of the
    /// microservice, only its root changes. Otherwise chaining through the deployed gateway,
    /// which matches on the upstream and not on the path of the microservice.
    /// </summary>
    private static string ResolveDownstreamPath(
        IConfigurationSection route,
        string upstream,
        LocalRoutingOptions options,
        Regex hostPattern)
    {
        var serviceKey = ExtractServiceKey(route[ConfigConstants.DownstreamHostKey], hostPattern);

        if (serviceKey is not null && options.ServicePrefixes.TryGetValue(serviceKey, out var prefix))
        {
            return $"{prefix}{route[ConfigConstants.DownstreamPathTemplateKey]}";
        }

        return $"{options.DeployedGatewayPrefix}{upstream}";
    }

    /// <summary>
    /// The App Services follow the appcegpulse{svc}{env_id}{instance} convention.
    /// The environment token is not substituted yet at this point, so both forms are accepted.
    ///
    /// Only the App Services are matched: the sole virtual machine downstream, the AI services,
    /// carries no public prefix and is therefore chained through the deployed gateway either way.
    /// </summary>
    private static Regex BuildHostPattern(string envId)
    {
        var environment = envId.Length == 0
            ? Regex.Escape(ConfigConstants.EnvIdToken)
            : $"(?:{Regex.Escape(ConfigConstants.EnvIdToken)}|{Regex.Escape(envId)})";

        return new Regex(
            $"^appcegpulse(?<svc>[a-z]+?){environment}(?<inst>\\d{{2}})\\.",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            HostPatternTimeout);
    }

    private static string? ExtractServiceKey(string? downstreamHost, Regex hostPattern)
    {
        if (string.IsNullOrWhiteSpace(downstreamHost))
        {
            return null;
        }

        var match = hostPattern.Match(downstreamHost);

        return match.Success ? $"{match.Groups["svc"].Value}{match.Groups["inst"].Value}" : null;
    }

    /// <summary>
    /// Target of a rewritten route: the root to reach, the downstream path when it changes,
    /// and whether the original scheme is kept for the WebSocket routes.
    /// </summary>
    private readonly record struct DownstreamTarget(Uri Root, string? Path, bool PreserveScheme);

    private static void ApplyTarget(
        IDictionary<string, string?> overrides,
        string routePath,
        DownstreamTarget target)
    {
        if (target.Path is not null)
        {
            overrides[$"{routePath}:{ConfigConstants.DownstreamPathTemplateKey}"] = target.Path;
        }

        overrides[$"{routePath}:{ConfigConstants.DownstreamHostKey}"] = target.Root.Host;
        overrides[$"{routePath}:{ConfigConstants.DownstreamPortKey}"] = target.Root.Port.ToString(CultureInfo.InvariantCulture);

        if (!target.PreserveScheme)
        {
            overrides[$"{routePath}:{ConfigConstants.DownstreamSchemeKey}"] = target.Root.Scheme;
        }
    }

    /// <summary>
    /// The service is the second segment of the upstream: /gtw/{service}/...
    /// Any query string is discarded before splitting.
    /// </summary>
    private static string? ExtractService(string? upstreamPathTemplate)
    {
        if (string.IsNullOrWhiteSpace(upstreamPathTemplate))
        {
            return null;
        }

        var path = upstreamPathTemplate.Split('?', 2)[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 2 ? segments[1] : null;
    }
}
