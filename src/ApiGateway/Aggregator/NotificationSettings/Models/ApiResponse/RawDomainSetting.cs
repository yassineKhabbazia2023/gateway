namespace ApiGateway.Aggregator.NotificationSettings.Models.ApiResponse
{
    public record RawDomainSetting(
       string Domain,
       List<RawSetting> Settings
   );
}
