namespace ApiGateway.Configuration;

public static class ConfigConstants
{
    public static readonly string OcelotConfigFile = "ocelot.json";
    public static readonly string OcelotConfigPath = "OCELOT_CONFIG_PATH";
    public static readonly int HttpClientRetryAttempt = 3;
    public static readonly int ProspectFinalizationRetryAttempt = 3;
    public static readonly int ProspectFinalizationRetryDelayMilliseconds = 1000;
    public static readonly int ProspectRoleSynchronizationRetryAttempt = 3;
    public static readonly int ProspectRoleSynchronizationRetryDelayMilliseconds = 1000;
}
