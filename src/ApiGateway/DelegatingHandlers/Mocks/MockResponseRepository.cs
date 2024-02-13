using System.Text.Json;
using System.IO.Abstractions;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseRepository : IMockResponseRepository
{
    private readonly IFileSystem _fileSystem;
    private readonly string _indexFileName;
    private Dictionary<string, string> RouteToFileMap { get; }
    public MockResponseRepository(IConfiguration configuration, IFileSystem fileSystem, IFileWatcherService fileWatcherService)
    {
        _fileSystem = fileSystem;
        var mocksFolderPath = configuration["MOCK_REPOSITORY_PATH"];
        Guard.Against.NullOrWhiteSpace(mocksFolderPath);
        _indexFileName = $"{mocksFolderPath}/mock_index.json";
        fileWatcherService.StartWatching(_indexFileName, LoadMappings);
        RouteToFileMap = new Dictionary<string, string>();
        LoadMappings();
    }

    private void LoadMappings()
    {
        RouteToFileMap.Clear();
        if (!_fileSystem.File.Exists(_indexFileName))
        {
            throw new FileNotFoundException("Cannot find the index file for mock_index.json");
        }
        var indexFileContent = _fileSystem.File.ReadAllText(_indexFileName);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var routeConfigs = JsonSerializer.Deserialize<List<MockedRouteConfig>>(indexFileContent, options);

        if (routeConfigs == null) return;

        foreach (var config in routeConfigs)
        {
            var key = $"{config.HttpVerb}:{config.DownstreamUri}".ToLower();
            RouteToFileMap[key] = Path.Combine(Path.GetDirectoryName(_indexFileName)!, config.ResponseMockJsonFile);
        }
    }

    public (bool Success, string FullPathFile) GetResponseFullPathFile(string routeKey)
    {
        if (!RouteToFileMap.TryGetValue(routeKey, out var filePath) || !_fileSystem.File.Exists(filePath))
        {
            return (false, null)!;
        }

        return (true, filePath);
    }
    
}