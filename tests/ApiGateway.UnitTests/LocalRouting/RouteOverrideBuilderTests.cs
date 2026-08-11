using ApiGateway.LocalRouting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.UnitTests.LocalRouting;

public class RouteOverrideBuilderTests
{
    private const string AccountHost = "appcegpulseacc#{env_id}#01.azurewebsites.net";
    private const string HistoryHost = "appcegpulsehis#{env_id}#01.azurewebsites.net";

    private static LocalRoutingOptions Options(params (string Service, string BaseUrl)[] locals)
    {
        var options = new LocalRoutingOptions
        {
            EnvId = "itg01",
            PublicBaseUrl = "https://api-itg01.itg.pulse.rydge.fr",
            DeployedGatewayPrefix = "/desktop",
            BaseUrl = "http://localhost:5080",
            ServicePrefixes = { ["acc01"] = "/account" },
        };

        foreach (var (service, baseUrl) in locals)
        {
            options.Services[service] = baseUrl;
        }

        return options;
    }

    private static IConfiguration Merged(params (string Upstream, string Downstream, string Scheme, string Host)[] routes)
    {
        var values = new Dictionary<string, string?>();

        for (var i = 0; i < routes.Length; i++)
        {
            values[$"Routes:{i}:UpstreamPathTemplate"] = routes[i].Upstream;
            values[$"Routes:{i}:DownstreamPathTemplate"] = routes[i].Downstream;
            values[$"Routes:{i}:DownstreamScheme"] = routes[i].Scheme;
            values[$"Routes:{i}:DownstreamHostAndPorts:0:Host"] = routes[i].Host;
            values[$"Routes:{i}:DownstreamHostAndPorts:0:Port"] = "443";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IConfiguration Raw(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void BuildRouteOverrides_WhenTheServiceHasAPublicPrefix_ShouldReachItDirectlyAndKeepItsPath()
    {
        // The currentuser fragment must survive: ContactHandler is the one translating it into
        // contactId, and that translation is only valid against the microservice itself.
        var merged = Merged((
            "/gtw/account/api/accounts/currentuser",
            "/api/accounts/currentuser",
            "https",
            AccountHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["Routes:0:DownstreamPathTemplate"].Should().Be("/account/api/accounts/currentuser");
        result.Overrides["Routes:0:DownstreamHostAndPorts:0:Host"].Should().Be("api-itg01.itg.pulse.rydge.fr");
        result.Overrides["Routes:0:DownstreamHostAndPorts:0:Port"].Should().Be("443");
        result.Overrides["Routes:0:DownstreamScheme"].Should().Be("https");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheServiceHasNoPublicPrefix_ShouldChainThroughTheDeployedGateway()
    {
        var merged = Merged((
            "/gtw/history/api/events",
            "/api/events",
            "https",
            HistoryHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["Routes:0:DownstreamPathTemplate"].Should().Be("/desktop/gtw/history/api/events");
        result.Overrides["Routes:0:DownstreamHostAndPorts:0:Host"].Should().Be("api-itg01.itg.pulse.rydge.fr");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheServiceIsLocal_ShouldPointToLocalhostAndKeepTheOriginalPath()
    {
        var merged = Merged((
            "/gtw/account/api/accounts/currentuser",
            "/api/accounts/currentuser",
            "https",
            AccountHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options(("account", "https://localhost:7248")));

        result.Overrides.Should().NotContainKey("Routes:0:DownstreamPathTemplate");
        result.Overrides["Routes:0:DownstreamHostAndPorts:0:Host"].Should().Be("localhost");
        result.Overrides["Routes:0:DownstreamHostAndPorts:0:Port"].Should().Be("7248");
        result.Overrides["Routes:0:DownstreamScheme"].Should().Be("https");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheSchemeIsWss_ShouldPreserveIt()
    {
        var merged = Merged(("/gtw/account/hub", "/hub", "wss", AccountHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides.Should().NotContainKey("Routes:0:DownstreamScheme");
    }

    [Fact]
    public void BuildRouteOverrides_ShouldChainKeepingTheUpstreamQueryString()
    {
        var merged = Merged((
            "/gtw/history/api/events?page={page}",
            "/api/events",
            "https",
            HistoryHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["Routes:0:DownstreamPathTemplate"]
            .Should().Be("/desktop/gtw/history/api/events?page={page}");
    }

    [Fact]
    public void BuildRouteOverrides_ShouldNeverTouchTheAggregates()
    {
        var values = new Dictionary<string, string?>
        {
            ["Aggregates:0:UpstreamPathTemplate"] = "/gtw/wallet/api/infos/currentuser",
            ["Aggregates:0:RouteKeys:0"] = "wallet",
        };
        var merged = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides.Keys.Should().NotContain(key => key.StartsWith("Aggregates", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildRouteOverrides_WhenADeclaredServiceDoesNotExist_ShouldReportIt()
    {
        var merged = Merged(("/gtw/account/api/accounts", "/api/accounts", "https", AccountHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options(("compte", "https://localhost:7248")));

        result.UnknownServices.Should().Equal("compte");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheRouteTableIsEmpty_ShouldNotThrowAndSetTheBaseUrl()
    {
        var merged = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["GlobalConfiguration:BaseUrl"].Should().Be("http://localhost:5080");
        result.UnknownServices.Should().BeEmpty();
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheHostDoesNotFollowTheNamingConvention_ShouldChain()
    {
        var merged = Merged((
            "/gtw/aiservices/api/chat",
            "/api/chat",
            "https",
            "un-hote-hors-convention.example.net"));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["Routes:0:DownstreamPathTemplate"].Should().Be("/desktop/gtw/aiservices/api/chat");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheRouteHasNoUpstream_ShouldIgnoreIt()
    {
        var merged = Raw(new Dictionary<string, string?>
        {
            ["Routes:0:DownstreamPathTemplate"] = "/api/events",
            ["Routes:0:DownstreamHostAndPorts:0:Host"] = HistoryHost,
        });

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides.Keys.Should().NotContain(key => key.StartsWith("Routes:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheUpstreamHasNoServiceSegment_ShouldIgnoreIt()
    {
        // A single segment: there is no /gtw/{service}/... to extract.
        var merged = Merged(("/gtw", "/api", "https", HistoryHost));

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides.Keys.Should().NotContain(key => key.StartsWith("Routes:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheRouteHasNoDownstreamHost_ShouldChain()
    {
        // Without a host there is no App Service code to match: chaining stays the only option.
        var merged = Raw(new Dictionary<string, string?>
        {
            ["Routes:0:UpstreamPathTemplate"] = "/gtw/history/api/events",
            ["Routes:0:DownstreamPathTemplate"] = "/api/events",
            ["Routes:0:DownstreamScheme"] = "https",
        });

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, Options());

        result.Overrides["Routes:0:DownstreamPathTemplate"].Should().Be("/desktop/gtw/history/api/events");
    }

    [Fact]
    public void BuildRouteOverrides_WhenTheEnvIdIsEmpty_ShouldMatchTheHostOnTheUnsubstitutedToken()
    {
        var merged = Merged((
            "/gtw/account/api/accounts",
            "/api/accounts",
            "https",
            AccountHost));

        var options = new LocalRoutingOptions
        {
            EnvId = string.Empty,
            PublicBaseUrl = "https://api-itg01.itg.pulse.rydge.fr",
            ServicePrefixes = { ["acc01"] = "/account" },
        };

        var result = RouteOverrideBuilder.BuildRouteOverrides(merged, options);

        result.Overrides["Routes:0:DownstreamPathTemplate"].Should().Be("/account/api/accounts");
    }
}
