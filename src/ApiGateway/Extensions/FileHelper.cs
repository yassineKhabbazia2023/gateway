using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.Extensions;

public static class FileHelper
{
    public static string GetOcelotConfigFullPathName(IConfiguration configuration)
    {
        return Path.Combine( GetStoragePath(configuration), ConfigConstants.OcelotConfigFile);
    }

    private static string GetStoragePath(IConfiguration configuration)
    {
         var configFullPathName = configuration[ConfigConstants.OcelotConfigPath];
        if (string.IsNullOrWhiteSpace(configFullPathName))
        {
            throw new NullReferenceException(configFullPathName);
        }
        return configFullPathName;
    }

    public static string GetLiteDbDir(IConfiguration configuration)
    {
        return Path.Combine( GetStoragePath(configuration),MocksConstants.FileDbName);
    }
}