namespace ApiGateway.DelegatingHandlers.Mocks;

public interface IMockResponseRepository
{
    ( bool Success, string FullPathFile) GetResponseFullPathFile(string routeKey);
}