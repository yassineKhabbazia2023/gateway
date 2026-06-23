using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.Booking;

public class BookingExperienceGuards(IOptionsMonitor<XpBookingOptions> options) : IBookingExperienceGuards
{
    public bool IsFeatureFlagEnabled() => options.CurrentValue.FeatureFlagEnabled;
}
