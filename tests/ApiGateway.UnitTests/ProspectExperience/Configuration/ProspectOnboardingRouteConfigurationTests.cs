using System.Text.Json;

namespace ApiGateway.UnitTests.ProspectExperience.Configuration;

/// <summary>
/// Tests Prospect onboarding Ocelot route configuration.
/// </summary>
public sealed class ProspectOnboardingRouteConfigurationTests
{
    /// <summary>
    /// Verifies that fetch onboarding steps uses accountId in the public and downstream route templates.
    /// </summary>
    [Fact]
    public void FetchStepsRoute_UsesAccountId()
    {
        using var document = LoadOcelotConfiguration();

        var route = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == "/gtw/prospect/api/onboarding/{accountId}/steps");

        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/prospects/{accountId}/steps");
    }

    /// <summary>
    /// Verifies that fetch beneficiaries uses accountId in the public and downstream route templates.
    /// </summary>
    [Fact]
    public void FetchBeneficiariesRoute_UsesAccountId()
    {
        using var document = LoadOcelotConfiguration();

        var route = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == "/gtw/prospect/api/onboarding/{accountId}/beneficiaries");

        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/prospects/{accountId}/beneficiaries");
    }

    /// <summary>
    /// Verifies that upload beneficiary identity documents uses accountId in the public and downstream route templates.
    /// </summary>
    [Fact]
    public void UploadBeneficiaryIdentityDocumentsRoute_UsesAccountId()
    {
        using var document = LoadOcelotConfiguration();

        var route = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == "/gtw/prospect/api/onboarding/{accountId}/beneficiaries/{beneficiaryId}/identity-documents");

        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/prospects/{accountId}/beneficiaries/{beneficiaryId}/identity-documents");
    }

    /// <summary>
    /// Verifies that delete prospect document uses accountId in the public and downstream route templates.
    /// </summary>
    [Fact]
    public void DeleteProspectDocumentRoute_UsesAccountId()
    {
        using var document = LoadOcelotConfiguration();

        var route = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == "/gtw/prospect/api/onboarding/{accountId}/documents/{documentId}");

        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/prospects/{accountId}/documents/{documentId}");
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
