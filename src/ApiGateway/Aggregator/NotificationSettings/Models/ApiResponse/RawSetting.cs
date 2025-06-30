namespace ApiGateway.Aggregator.NotificationSettings.Models.ApiResponse
{
    public record RawSetting(
        int Id,
        string Label,
        bool Value
    );
}
