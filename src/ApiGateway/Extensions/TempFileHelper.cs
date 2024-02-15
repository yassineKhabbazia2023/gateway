using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.Extensions;

public static class TempFileHelper
{
    public static string GetOcelotTempDir()
    {
        var tempPath = Path.GetTempPath();
        var tempFilePath = Path.Combine(tempPath, ConfigConstants.OcelotConfigFile);
        return tempFilePath;

    }

    public static string GetLiteDbTempDir()
    {
          var tempPath = Path.GetTempPath();
          var tempFilePath = Path.Combine(tempPath, MocksConstants.FileDbName);
          return tempFilePath;
    }
}