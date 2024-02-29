using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Exceptions;

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
            throw new InvalidConfigException(configFullPathName);
        }
        return configFullPathName;
    }

    public static string GetLiteDbDir(IConfiguration configuration)
    {
        return Path.Combine( GetStoragePath(configuration),MocksConstants.FileDbName);
    }
}