using ApiGateway.FeatureFlags.Models;

namespace ApiGateway.UnitTests.FeatureFlags;

public class FeatureContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromEmail_WhenEmailIsBlank_ShouldReturnNull(string? email)
    {
        FeatureContext.FromEmail(email).Should().BeNull();
    }

    [Fact]
    public void FromEmail_WhenEmailHasSurroundingWhitespace_ShouldTrim()
    {
        var context = FeatureContext.FromEmail("  user@test.fr  ");

        context.Should().NotBeNull();
        context!.Email.Should().Be("user@test.fr");
    }
}
