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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromContactId_WhenContactIdIsBlank_ShouldReturnNull(string? contactId)
    {
        FeatureContext.FromContactId(contactId).Should().BeNull();
    }

    [Fact]
    public void FromContactId_WhenContactIdHasSurroundingWhitespace_ShouldTrim()
    {
        var context = FeatureContext.FromContactId("  12345  ");

        context.Should().NotBeNull();
        context!.ContactId.Should().Be("12345");
    }
}
