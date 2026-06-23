using ApiGateway.Booking;
using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.UnitTests.Booking;

public class BookingExperienceGuardsTests
{
    private static BookingExperienceGuards CreateService(bool featureFlagEnabled)
    {
        var monitor = new Mock<IOptionsMonitor<XpBookingOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new XpBookingOptions { FeatureFlagEnabled = featureFlagEnabled });
        return new BookingExperienceGuards(monitor.Object);
    }

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
