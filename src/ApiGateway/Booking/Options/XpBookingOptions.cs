namespace ApiGateway.Booking.Options;

public class XpBookingOptions
{
    public bool FeatureFlagEnabled { get; set; }
    public int SyncIntervalMinutes { get; set; } = 5;
}
