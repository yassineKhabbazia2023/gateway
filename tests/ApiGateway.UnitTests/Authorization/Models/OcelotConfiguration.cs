namespace ApiGateway.UnitTests.Authorization
{
    internal class OcelotConfiguration
    {
        public IList<Route> Routes { get; set; }
    }
    internal class Route
    {
        public string UpstreamPathTemplate { get; set; }
        public IList<string> UpstreamHttpMethod { get; set; }

        public IDictionary<string, string> RouteClaimsRequirement { get; set; }
    }
    internal class Routes
    {
        public string Route { get; set; }
        public IList<string> Claims { get; set; }
    }

}
