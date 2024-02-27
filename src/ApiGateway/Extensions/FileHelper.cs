using ApiGateway.Configuration;
using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.Extensions;

/// <summary>
/// Note this is a temp solution to by pass how webApp is deployed (linux + docker) 
/// </summary>
public static class FileHelper
{
    private static string? _configFullPathName;
    public static string GetOcelotConfigFullPathName(IConfiguration configuration)
    {
        return Path.Combine( GetStoragePath(configuration), ConfigConstants.OcelotConfigFile);
    }

    private static string GetStoragePath(IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(_configFullPathName))
            return  _configFullPathName;
        _configFullPathName = configuration[ConfigConstants.OcelotConfigPath];
        if (string.IsNullOrWhiteSpace(_configFullPathName))
        {
            throw new NullReferenceException(_configFullPathName);
        }
        return _configFullPathName;
    }

    public static string GetLiteDbDir(IConfiguration configuration)
    {
        return Path.Combine( GetStoragePath(configuration),MocksConstants.FileDbName);
    }
}