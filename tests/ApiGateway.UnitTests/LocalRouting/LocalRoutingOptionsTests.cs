using ApiGateway.LocalRouting;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests.LocalRouting;

public class LocalRoutingOptionsTests
{
    [Theory]
    [InlineData("itg01", "itg")]
    [InlineData("rec02", "rec")]
    [InlineData("prd", "prd")]
    public void Env_ShouldTakeTheFirstThreeCharactersOfTheEnvId(string envId, string expected)
    {
        new LocalRoutingOptions { EnvId = envId }.Env.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("it")]
    public void Env_WhenTheEnvIdIsShorterThanThreeCharacters_ShouldReturnItAsIs(string envId)
    {
        new LocalRoutingOptions { EnvId = envId }.Env.Should().Be(envId);
    }
}
