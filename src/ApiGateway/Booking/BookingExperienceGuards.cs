using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.Booking;

public class BookingExperienceGuards(IOptions<XpBookingOptions> options) : IBookingExperienceGuards
{
    public bool IsFeatureFlagEnabled() => options.Value.FeatureFlagEnabled;
}
