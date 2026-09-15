using System.Text.Json;

namespace ApiGateway.UnitTests.Registry.Configuration;

/// <summary>
/// Tests the HubSpot submissions QA reset Ocelot route configuration.
/// </summary>
public sealed class HubSpotSubmissionsResetRouteConfigurationTests
{
    private const string DeleteSubmissionsUpstream = "/gtw/registry/api/accounts/{accountId}/hubspot/submissions";

    /// <summary>
    /// Verifies that resetting an account's HubSpot submissions is routed to the Registry service,
    /// reachable only by Azure AD (collaborator/QA) authentication, and locked behind the
    /// QA-sensitive endpoints handler.
    /// </summary>
    [Fact]
    public void DeleteSubmissionsRoute_IsRoutedToRegistryAndLockedForQa()
    {
        using var document = LoadOcelotConfiguration();

        var route = GetRoute(document, DeleteSubmissionsUpstream, "DELETE");

        route.GetProperty("SwaggerKey").GetString().Should().Be("ContactRegistry");
        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/hubspot/accounts/{accountId}/submissions");
        route.GetProperty("DownstreamScheme").GetString().Should().Be("https");

        GetMethods(route).Should().Equal("DELETE");
        GetAuthenticationProviderKeys(route).Should().Equal("AAD");
        GetHosts(route).Should().OnlyContain(host => host.Contains("reg", StringComparison.OrdinalIgnoreCase));
        GetDelegatingHandlers(route).Should().Contain("QaSensitiveEndpointsHandler");
    }

    /// <summary>
    /// Finds the route matching both the upstream path and HTTP method: Ocelot allows several
    /// routes to share the same <c>UpstreamPathTemplate</c> as long as they are differentiated
    /// by <c>UpstreamHttpMethod</c> (e.g. GET vs DELETE on the same submissions resource).
    /// </summary>
    private static JsonElement GetRoute(JsonDocument document, string upstreamPathTemplate, string upstreamHttpMethod)
    {
        return document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == upstreamPathTemplate &&
                GetMethods(candidate).Contains(upstreamHttpMethod));
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

    private static List<string> GetDelegatingHandlers(JsonElement route)
    {
        return route.TryGetProperty("DelegatingHandlers", out var handlers)
            ? handlers.EnumerateArray().Select(handler => handler.GetString()!).ToList()
            : new List<string>();
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
