namespace ApiGateway.Booking.Options;

public class XpBookingOptions
{
    public bool FeatureFlagEnabled { get; set; }
    public string Whitelist { get; set; } = string.Empty;
}
