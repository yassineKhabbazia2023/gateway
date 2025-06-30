namespace ApiGateway.Aggregator.NotificationSettings.Models
{
    public record SettingsResult(
        List<DomainSetting> DomainSettings,
        PreferenceSettings PreferenceSettings
    );
}
