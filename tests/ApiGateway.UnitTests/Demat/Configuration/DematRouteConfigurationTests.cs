using System.Text.Json;

namespace ApiGateway.UnitTests.Demat.Configuration;

/// <summary>
/// Tests Demat (mail collect) Ocelot route configuration.
/// </summary>
public sealed class DematRouteConfigurationTests
{
    private const string GetCurrentUserUpstream = "/gtw/demat/api/accounts/{accountId}/currentuser";
    private const string PostHubspotSubmissionsUpstream = "/gtw/demat/api/accounts/{accountId}/hubspot/submissions/currentuser";
    private const string PostDematReadyClosureUpstream = "/gtw/demat/api/accounts/{accountId}/demat-ready/closure";
    private const string FeatureFlagKey = "isDematMailCollectEnabled";

    /// <summary>
    /// Verifies that fetching the current user's Demat info is routed to the Account service,
    /// gated by the Demat feature flag, and reachable only by client authentication.
    /// </summary>
    [Fact]
    public void GetCurrentUserRoute_IsRoutedToAccountAndClientOnly()
    {
        using var document = LoadOcelotConfiguration();

        var route = GetRoute(document, GetCurrentUserUpstream);

        route.GetProperty("SwaggerKey").GetString().Should().Be("Account");
        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/accounts/{accountId}/currentuser");
        route.GetProperty("DownstreamScheme").GetString().Should().Be("https");

        GetMethods(route).Should().Equal("GET");
        GetAuthenticationProviderKeys(route).Should().Equal("GIGYA MyPulse v2");
        GetHosts(route).Should().OnlyContain(host => host.Contains("acc", StringComparison.OrdinalIgnoreCase));
        GetFeatureFlag(route).Should().Be(FeatureFlagKey);
    }

    /// <summary>
    /// Verifies that submitting the current user's Hubspot Demat submission is routed to the
    /// Registry service, gated by the Demat feature flag, and reachable only by client authentication.
    /// </summary>
    [Fact]
    public void PostHubspotSubmissionsRoute_IsRoutedToRegistryAndClientOnly()
    {
        using var document = LoadOcelotConfiguration();

        var route = GetRoute(document, PostHubspotSubmissionsUpstream);

        route.GetProperty("SwaggerKey").GetString().Should().Be("ContactRegistry");
        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/hubspot/accounts/{accountId}/submissions");
        route.GetProperty("DownstreamScheme").GetString().Should().Be("https");

        GetMethods(route).Should().Equal("POST");
        GetAuthenticationProviderKeys(route).Should().Equal("GIGYA MyPulse v2");
        GetHosts(route).Should().OnlyContain(host => host.Contains("reg", StringComparison.OrdinalIgnoreCase));
        GetFeatureFlag(route).Should().Be(FeatureFlagKey);
    }

    /// <summary>
    /// Verifies that closing an account's demat-ready status is routed to the Account service,
    /// gated by the Demat feature flag, and reachable only by client authentication.
    /// </summary>
    [Fact]
    public void PostDematReadyClosureRoute_IsRoutedToAccountAndClientOnly()
    {
        using var document = LoadOcelotConfiguration();

        var route = GetRoute(document, PostDematReadyClosureUpstream);

        route.GetProperty("SwaggerKey").GetString().Should().Be("Account");
        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/accounts/{accountId}/demat-ready/closure");
        route.GetProperty("DownstreamScheme").GetString().Should().Be("https");

        GetMethods(route).Should().Equal("POST");
        GetAuthenticationProviderKeys(route).Should().Equal("GIGYA MyPulse v2");
        GetHosts(route).Should().OnlyContain(host => host.Contains("acc", StringComparison.OrdinalIgnoreCase));
        GetFeatureFlag(route).Should().Be(FeatureFlagKey);
    }

    private static JsonElement GetRoute(JsonDocument document, string upstreamPathTemplate)
    {
        return document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("UpstreamPathTemplate").GetString() == upstreamPathTemplate);
    }

    private static List<string> GetMethods(JsonElement route)
    {
        return route.GetProperty("UpstreamHttpMethod")
            .EnumerateArray()
            .Select(method => method.GetString()!)
            .ToList();
    }

    private static List<string> GetAuthenticationProviderKeys(JsonElement route)
    {
        return route.GetProperty("AuthenticationOptions").GetProperty("AuthenticationProviderKeys")
            .EnumerateArray()
            .Select(key => key.GetString()!)
            .ToList();
    }

    private static List<string> GetHosts(JsonElement route)
    {
        return route.GetProperty("DownstreamHostAndPorts")
            .EnumerateArray()
            .Select(hostAndPort => hostAndPort.GetProperty("Host").GetString()!)
            .ToList();
    }

    private static string? GetFeatureFlag(JsonElement route)
    {
        return route.TryGetProperty("Metadata", out var metadata) && metadata.TryGetProperty("featureFlag", out var flag)
            ? flag.GetString()
            : null;
    }

    /// <summary>
    /// Loads the production Ocelot configuration by merging the per-domain
    /// <c>ocelot.*.json</c> files (see <c>src/Config/README.md</c> and
    /// <c>pipelines/Deployment/Public/Merge-OcelotConfig.ps1</c>), which the
    /// build pipeline concatenates into the single <c>ocelot.json</c> consumed
    /// at runtime.
    /// </summary>
    /// <returns>The parsed, merged Ocelot JSON document.</returns>
    private static JsonDocument LoadOcelotConfiguration()
    {
        var configFolder = Path.Combine(FindRepositoryRoot(), "src", "Config");

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("Routes");

            foreach (var file in Directory
                         .GetFiles(configFolder, "ocelot.*.json")
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.TryGetProperty("Routes", out var routes) &&
                    routes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var route in routes.EnumerateArray())
                    {
                        route.WriteTo(writer);
                    }
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.ToArray());
    }

    /// <summary>
    /// Walks up from the test output directory to the repository root
    /// (the folder that contains both <c>src</c> and <c>tests</c>).
    /// </summary>
    /// <returns>The absolute path of the repository root.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.Exists(Path.Combine(directory, "src")) &&
                Directory.Exists(Path.Combine(directory, "tests")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root containing 'src' and 'tests'.");
    }
}
