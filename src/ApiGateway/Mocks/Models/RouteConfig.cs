namespace ApiGateway.Mocks.Models;

public class RouteConfig
{
    public required List<RouteDefinition> Routes { get; set; }
    public required GlobalConfiguration GlobalConfiguration { get; set; }
}
public class RouteDefinition
{
    public required string DownstreamPathTemplate { get; set; }
    public required string DownstreamScheme { get; set; }
    public required List<HostAndPort> DownstreamHostAndPorts { get; set; }
    public required string UpstreamPathTemplate { get; set; }
    public required List<string> UpstreamHttpMethod { get; set; }
}

public class HostAndPort
{
    public required string Host { get; set; }
    public int Port { get; set; }
}

public class GlobalConfiguration
{
    public required string BaseUrl { get; set; }
}