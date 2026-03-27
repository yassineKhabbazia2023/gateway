using ApiGateway.Configuration;
using ApiGateway.Exceptions;

namespace ApiGateway.Helpers;

public static class FileHelper
{
    public static string GetOcelotConfigFullPathName(IConfiguration configuration)
    {
        return Path.Combine(GetStoragePath(configuration), ConfigConstants.OcelotConfigFile);
    }

    private static string GetStoragePath(IConfiguration configuration)
    {
        var configFullPathName = configuration[ConfigConstants.OcelotConfigPath];
        if (string.IsNullOrWhiteSpace(configFullPathName))
        {
            throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullConfigurationCode, string.Format(Errors.NullConfigurationMessage, nameof(configFullPathName)));
        }
        return configFullPathName;
    }
}