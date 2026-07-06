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
    /// Loads the production Ocelot configuration.
    /// </summary>
    /// <returns>The parsed Ocelot JSON document.</returns>
    private static JsonDocument LoadOcelotConfiguration()
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

        return JsonDocument.Parse(File.ReadAllText(configPath));
    }
}
