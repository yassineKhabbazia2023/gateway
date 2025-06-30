using ApiGateway.Aggregator.NotificationSettings.Models;

namespace ApiGateway.Aggregator.NotificationSettings.Models.ApiResponse
{
    public record RawResponse(
        List<RawDomainSetting> DomainSettings,
        PreferenceSettings PreferenceSettings
    );
}
