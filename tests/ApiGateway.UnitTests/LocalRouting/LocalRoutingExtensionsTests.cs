using ApiGateway.Exceptions;
using ApiGateway.LocalRouting;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ApiGateway.UnitTests.LocalRouting;

/// <summary>
/// The nominal cases go through a real configuration folder: the Ocelot merge reads files,
/// it cannot be mocked.
/// </summary>
public sealed class LocalRoutingExtensionsTests : IDisposable
{
    private const string AccountHost = "appcegpulseacc#{env_id}#01.azurewebsites.net";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        $"local-routing-{Guid.NewGuid():N}");

    public LocalRoutingExtensionsTests()
    {
        Directory.CreateDirectory(_folder);

        File.WriteAllText(
            Path.Combine(_folder, "ocelot.global.json"),
            """
            {
              "GlobalConfiguration": {
                "BaseUrl": "https://api-#{env_id}#.#{env}#.example.net"
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(_folder, "ocelot.account.json"),
            $$"""
            {
              "Routes": [
                {
                  "Key": "account-#{env_id}#",
                  "UpstreamPathTemplate": "/gtw/account/api/accounts",
                  "DownstreamPathTemplate": "/api/accounts",
                  "DownstreamScheme": "https",
                  "DownstreamHostAndPorts": [ { "Host": "{{AccountHost}}", "Port": 443 } ]
                }
              ]
            }
            """);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static IWebHostEnvironment Environment(string contentRootPath)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(contentRootPath);
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        environment.SetupGet(e => e.ApplicationName).Returns("ApiGateway");

        return environment.Object;
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> ValidSection() => new()
    {
        ["LocalRouting:EnvId"] = "itg01",
        ["LocalRouting:PublicBaseUrl"] = "https://api-itg01.itg.pulse.rydge.fr",
        ["LocalRouting:DeployedGatewayPrefix"] = "/desktop",
        ["LocalRouting:ConfigFolder"] = ".",
        ["LocalRouting:BaseUrl"] = "http://localhost:5080",
        ["LocalRouting:ServicePrefixes:acc01"] = "/account",
    };

    private IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddLocalRouting(Configuration(values), Environment(_folder))
            .Build();

    [Fact]
    public void AddLocalRouting_WhenTheBuilderIsNull_ShouldThrow()
    {
        var action = () => LocalRoutingExtensions.AddLocalRouting(
            null!,
            Configuration(ValidSection()),
            Environment(_folder));

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLocalRouting_WhenTheConfigurationIsNull_ShouldThrow()
    {
        var action = () => new ConfigurationBuilder().AddLocalRouting(null!, Environment(_folder));

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLocalRouting_WhenTheEnvironmentIsNull_ShouldThrow()
    {
        var action = () => new ConfigurationBuilder().AddLocalRouting(Configuration(ValidSection()), null!);

        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// AddLocalRouting runs before builder.Build(), so these two guards fail the startup and
    /// their status never reaches a caller. It is asserted all the same, to keep the three
    /// configuration guards of the gateway on the same code and the same status.
    /// </summary>
    [Fact]
    public void AddLocalRouting_WhenTheSectionIsMissing_ShouldThrowAGatewayException()
    {
        var action = () => Build([]);

        var exception = action.Should().Throw<GatewayException>().Which;

        exception.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        exception.ErrorCode.Should().Be(Errors.NullConfigurationCode);
        exception.Message.Should().Contain(LocalRoutingOptions.SectionName);
    }

    [Fact]
    public void AddLocalRouting_WhenThePublicBaseUrlIsNotSet_ShouldThrowAGatewayException()
    {
        var values = ValidSection();
        values["LocalRouting:PublicBaseUrl"] = "  ";

        var action = () => Build(values);

        var exception = action.Should().Throw<GatewayException>().Which;

        exception.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        exception.ErrorCode.Should().Be(Errors.NullConfigurationCode);
        exception.Message.Should().Contain("PublicBaseUrl");
    }

    [Fact]
    public void AddLocalRouting_ShouldSubstituteTheEnvironmentTokens()
    {
        var configuration = Build(ValidSection());

        configuration["Routes:0:Key"].Should().Be("account-itg01");
    }

    [Fact]
    public void AddLocalRouting_ShouldRewriteTheDownstreamAndTheLocalBaseUrl()
    {
        var configuration = Build(ValidSection());

        configuration["Routes:0:DownstreamPathTemplate"].Should().Be("/account/api/accounts");
        configuration["Routes:0:DownstreamHostAndPorts:0:Host"].Should().Be("api-itg01.itg.pulse.rydge.fr");
        configuration["GlobalConfiguration:BaseUrl"].Should().Be("http://localhost:5080");
    }

    [Fact]
    public void AddLocalRouting_WhenTheServiceRunsOnTheWorkstation_ShouldPointToIt()
    {
        var values = ValidSection();
        values["LocalRouting:Services:account"] = "https://localhost:7248";

        var configuration = Build(values);

        configuration["Routes:0:DownstreamHostAndPorts:0:Host"].Should().Be("localhost");
        configuration["Routes:0:DownstreamHostAndPorts:0:Port"].Should().Be("7248");
        configuration["Routes:0:DownstreamPathTemplate"].Should().Be("/api/accounts");
    }

    [Fact]
    public void AddLocalRouting_WhenADeclaredServiceMatchesNoRoute_ShouldNotThrow()
    {
        var values = ValidSection();
        values["LocalRouting:Services:compte"] = "https://localhost:7248";

        var action = () => Build(values);

        action.Should().NotThrow();
    }
}
