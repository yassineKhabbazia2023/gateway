namespace ApiGateway.DelegatingHandlers.Mocks;

public record MockedRouteConfig(string HttpVerb, string DownstreamUri, string ResponseMockJsonFile)
{
    
}
