using ApiGateway.Booking;
using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.UnitTests.Booking;

public class BookingExperienceGuardsTests
{
    private static BookingExperienceGuards CreateService(bool featureFlagEnabled) =>
        new(Options.Create(new XpBookingOptions { FeatureFlagEnabled = featureFlagEnabled }));

    [Fact]
    public void IsFeatureFlagEnabled_WhenEnabled_ReturnsTrue()
    {
        var service = CreateService(featureFlagEnabled: true);
        service.IsFeatureFlagEnabled().Should().BeTrue();
    }

    [Fact]
    public void IsFeatureFlagEnabled_WhenDisabled_ReturnsFalse()
    {
        var service = CreateService(featureFlagEnabled: false);
        service.IsFeatureFlagEnabled().Should().BeFalse();
    }
}
