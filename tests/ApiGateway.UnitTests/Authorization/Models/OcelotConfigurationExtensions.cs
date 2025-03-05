
namespace ApiGateway.UnitTests.Authorization
{
    internal static class OcelotConfigurationExtensions
    {
        internal static IEnumerable<Routes> GetFlattenedRoutes(this OcelotConfiguration ocelotConfig)
        {
            return ocelotConfig.Routes.SelectMany(route =>
            {
                return route.UpstreamHttpMethod.Select(method =>
                {
                    if (route.RouteClaimsRequirement is null)
                    {
                        return new Routes
                        {
                            Route = method + " " + route.UpstreamPathTemplate,
                        };
                    }
                    else
                    {
                        route.RouteClaimsRequirement.TryGetValue(method, out var routeClaimsRequirement);
                        if (routeClaimsRequirement is not null)
                        {
                            return new Routes
                            {
                                Route = method + " " + route.UpstreamPathTemplate,
                                Claims = routeClaimsRequirement.Split(",")
                            };
                        }
                        else
                        {
                            return new Routes
                            {
                                Route = method + " " + route.UpstreamPathTemplate,
                            };
                        }
                    }
                });
            }).OrderBy(d => d.Route);
        }
    }
}
