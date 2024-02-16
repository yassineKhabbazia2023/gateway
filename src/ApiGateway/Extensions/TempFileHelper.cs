using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.Extensions;

public static class TempFileHelper
{
    private static string? ocelotTempDir;
    private static string? _liteDbTempDir;

    public static string GetOcelotTempDir()
    {
        if (string.IsNullOrEmpty(ocelotTempDir))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath); // Ensure the directory is created
            ocelotTempDir = Path.Combine(tempPath, ConfigConstants.OcelotConfigFile);
        }
        return ocelotTempDir;
    }

    public static string GetLiteDbTempDir()
    {
        if (string.IsNullOrEmpty(_liteDbTempDir))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);
            _liteDbTempDir = Path.Combine(tempPath, MocksConstants.FileDbName);
        }
        return _liteDbTempDir;
    }
}