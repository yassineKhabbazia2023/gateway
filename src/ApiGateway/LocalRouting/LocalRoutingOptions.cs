namespace ApiGateway.LocalRouting;

/// <summary>
/// Local routing settings, bound from the <see cref="SectionName"/> section.
/// Fed by appsettings.Development.json (shared values) and appsettings.local.json
/// (services started locally, not versioned).
/// </summary>
public sealed class LocalRoutingOptions
{
    public const string SectionName = "LocalRouting";

    /// <summary>Target environment identifier, for example itg01. Substitutes #{env_id}#.</summary>
    public string EnvId { get; init; } = string.Empty;

    /// <summary>Public root reachable from a workstation, without a trailing slash.</summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>Prefix under which the public entry point exposes the deployed gateway.</summary>
    public string DeployedGatewayPrefix { get; init; } = "/desktop";

    /// <summary>Folder holding the ocelot.*.json files, relative to the content root.</summary>
    public string ConfigFolder { get; init; } = "../Config";

    /// <summary>GlobalConfiguration.BaseUrl of the local gateway.</summary>
    public string BaseUrl { get; init; } = "http://localhost:5080";

    /// <summary>Services started on the workstation: key = service segment, value = root URL.</summary>
    public Dictionary<string, string> Services { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Public prefixes, by App Service code ({svc}{instance}, for example acc01).
    /// A service listed here is reached directly, with the prefix in front of its original
    /// path; the others are chained through the deployed gateway.
    ///
    /// This map is maintained by the infrastructure team and changes: see
    /// doc/Run-Local-Gateway.md for the diagnostic recipe when a route starts returning an
    /// nginx 404.
    /// </summary>
    public Dictionary<string, string> ServicePrefixes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Environment type derived from <see cref="EnvId"/>. Substitutes #{env}#.</summary>
    public string Env => EnvId.Length >= 3 ? EnvId[..3] : EnvId;
}
