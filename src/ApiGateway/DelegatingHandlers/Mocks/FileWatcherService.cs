namespace ApiGateway.DelegatingHandlers.Mocks;

public class FileWatcherService : IFileWatcherService, IDisposable
{
    private FileSystemWatcher? _watcher;

    public void StartWatching(string path, Action onChange)
    {
        Guard.Against.NullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(path);
        var filter = Path.GetFileName(path);

        _watcher = new FileSystemWatcher(directory!)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = filter
        };

        _watcher.Changed += (_, _) => onChange();
        _watcher.Created += (_, _) => onChange();
        _watcher.Deleted += (_, _) => onChange();
        _watcher.Renamed += (_, _) => onChange();

        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
    }

    public void Dispose()
    {
        StopWatching();
    }
}