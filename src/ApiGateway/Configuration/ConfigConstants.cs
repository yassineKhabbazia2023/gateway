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

    // Keys of the Ocelot configuration, as read from the merged ocelot.*.json files.
    public static readonly string WebSocketScheme = "wss";
    public static readonly string RoutesSection = "Routes";
    public static readonly string UpstreamPathTemplateKey = "UpstreamPathTemplate";
    public static readonly string DownstreamPathTemplateKey = "DownstreamPathTemplate";
    public static readonly string DownstreamSchemeKey = "DownstreamScheme";
    public static readonly string DownstreamHostKey = "DownstreamHostAndPorts:0:Host";
    public static readonly string DownstreamPortKey = "DownstreamHostAndPorts:0:Port";
    public static readonly string GlobalBaseUrlKey = "GlobalConfiguration:BaseUrl";

    // Tokens that the deployment pipeline substitutes, resolved in memory for a local run.
    public static readonly string EnvIdToken = "#{env_id}#";
    public static readonly string EnvToken = "#{env}#";

    // The Gigya api key identifies the site and appears in the fetch URL of the public JWK as well
    // as in the issuer of every customer token. It is declared once, under GigyaApiKeyConfigKey,
    // and referenced through GigyaApiKeyToken everywhere else.
    public static readonly string GigyaApiKeyConfigKey = "GigyaApiKey";
    public static readonly string GigyaApiKeyToken = "#{gigya_api_key}#";
}
