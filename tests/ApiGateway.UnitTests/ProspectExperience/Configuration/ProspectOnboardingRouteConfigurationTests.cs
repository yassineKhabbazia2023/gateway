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
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Config",
            "ocelot.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));

        var route = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("UpstreamPathTemplate").GetString() == "/gtw/prospect/api/onboarding/{accountId}/steps");

        route.GetProperty("DownstreamPathTemplate").GetString().Should().Be("/api/prospects/{accountId}/steps");
    }
}
