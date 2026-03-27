namespace ApiGateway.DelegatingHandlers.Mocks;

public interface IMockResponseRepository
{
    Task<(bool Success, string? JsonContent)> GetJsonContentAsync(string routeKey);
    Task UpsertAsync(string routeKey, string jsonContent);
    Task<bool> DeleteAsync(string routeKey);
    Task<IReadOnlyList<MockEntry>> ListAllAsync();
}

public record MockEntry(string RouteKey, string JsonContent);
