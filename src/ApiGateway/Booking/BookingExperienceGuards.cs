using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.Booking;

public class BookingExperienceGuards(IOptions<XpBookingOptions> options) : IBookingExperienceGuards
{
    private readonly HashSet<string> _whitelist = options.Value.Whitelist
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool HasAccess(string email) => _whitelist.Contains(email);

    public bool IsFeatureFlagEnabled() => options.Value.FeatureFlagEnabled;
}
