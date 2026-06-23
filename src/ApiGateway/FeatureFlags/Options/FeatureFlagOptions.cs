namespace ApiGateway.FeatureFlags.Options;

public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    public ConfigCatSettings ConfigCat { get; set; } = new();
}

public sealed class ConfigCatSettings
{
    public string SdkKey { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 60;
    public string BaseUrl { get; set; } = "https://cdn-eu.configcat.com";
}
