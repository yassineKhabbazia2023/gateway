using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.Extensions;

/// <summary>
/// Note this is a temp solution to by pass how webApp is deployed (linux + docker) 
/// </summary>
public static class TempFileHelper
{
    private static string? _ocelotTempDir;
    private static string? _liteDbTempDir;

    public static string GetOcelotTempDir()
    {
        if (!string.IsNullOrEmpty(_ocelotTempDir)) return _ocelotTempDir;
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath); // Ensure the directory is created
        _ocelotTempDir = Path.Combine(tempPath, ConfigConstants.OcelotConfigFile);
        return _ocelotTempDir;
    }

    public static string GetLiteDbTempDir()
    {
        if (!string.IsNullOrEmpty(_liteDbTempDir)) return _liteDbTempDir;
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        _liteDbTempDir = Path.Combine(tempPath, MocksConstants.FileDbName);
        return _liteDbTempDir;
    }
}