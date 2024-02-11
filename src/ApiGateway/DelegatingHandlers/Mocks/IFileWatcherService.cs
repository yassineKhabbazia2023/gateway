namespace ApiGateway.DelegatingHandlers.Mocks;

public interface IFileWatcherService
{
    void StartWatching(string path, Action onChange);
    void StopWatching();
}