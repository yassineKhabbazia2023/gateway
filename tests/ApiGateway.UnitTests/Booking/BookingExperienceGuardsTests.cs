using ApiGateway.Booking;
using ApiGateway.Booking.Options;
using Microsoft.Extensions.Options;

namespace ApiGateway.UnitTests.Booking;

public class BookingExperienceGuardsTests
{
    private static BookingExperienceGuards CreateService(string whitelist, bool featureFlagEnabled = true) =>
        new(Options.Create(new XpBookingOptions { Whitelist = whitelist, FeatureFlagEnabled = featureFlagEnabled }));

    [Fact]
    public void HasAccess_UserInWhitelist_ReturnsTrue()
    {
        var service = CreateService("user@test.fr, other@test.fr");
        service.HasAccess("user@test.fr").Should().BeTrue();
    }

    [Fact]
    public void HasAccess_UserNotInWhitelist_ReturnsFalse()
    {
        var service = CreateService("user@test.fr, other@test.fr");
        service.HasAccess("notlisted@test.fr").Should().BeFalse();
    }

    [Fact]
    public void HasAccess_EmptyWhitelist_ReturnsFalse()
    {
        var service = CreateService("");
        service.HasAccess("user@test.fr").Should().BeFalse();
    }

    [Fact]
    public void HasAccess_WhitelistWithSpaces_TrimsAndMatchesCorrectly()
    {
        var service = CreateService("  user@test.fr  ,  other@test.fr  ");
        service.HasAccess("user@test.fr").Should().BeTrue();
    }

    [Fact]
    public void HasAccess_CaseInsensitiveMatch_ReturnsTrue()
    {
        var service = CreateService("user@test.fr");
        service.HasAccess("User@Test.FR").Should().BeTrue();
    }

    [Fact]
    public void IsFeatureFlagEnabled_WhenEnabled_ReturnsTrue()
    {
        var service = CreateService("", featureFlagEnabled: true);
        service.IsFeatureFlagEnabled().Should().BeTrue();
    }

    [Fact]
    public void IsFeatureFlagEnabled_WhenDisabled_ReturnsFalse()
    {
        var service = CreateService("", featureFlagEnabled: false);
        service.IsFeatureFlagEnabled().Should().BeFalse();
    }
}
