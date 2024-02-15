namespace ApiGateway.DelegatingHandlers.Mocks;

public interface IMockResponseRepository
{
    (bool, string? JsonContent) GetJsonContent(string routeKey);
}