using ApiGateway.LocalRouting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.UnitTests.LocalRouting;

public class TokenSubstitutionTests
{
    private static readonly LocalRoutingOptions Options = new() { EnvId = "itg01" };

    private static IConfiguration Config(params (string Key, string Value)[] entries)
    {
        var values = entries.ToDictionary(entry => entry.Key, entry => (string?)entry.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void BuildTokenOverrides_ShouldReplaceBothTokens()
    {
        var config = Config(("SwaggerEndPoints:0:Config:0:Url",
            "https://appcegpulseacc#{env_id}#01.#{env}#.example/swagger/v1/swagger.json"));

        var result = TokenSubstitution.BuildTokenOverrides(config, Options);

        result["SwaggerEndPoints:0:Config:0:Url"]
            .Should().Be("https://appcegpulseaccitg0101.itg.example/swagger/v1/swagger.json");
    }

    [Fact]
    public void BuildTokenOverrides_WhenAValueCarriesNoToken_ShouldIgnoreIt()
    {
        var config = Config(("Routes:0:DownstreamScheme", "https"));

        var result = TokenSubstitution.BuildTokenOverrides(config, Options);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildTokenOverrides_WhenTheConfigurationIsEmpty_ShouldReturnAnEmptyDictionary()
    {
        var result = TokenSubstitution.BuildTokenOverrides(Config(), Options);

        result.Should().BeEmpty();
    }
}
