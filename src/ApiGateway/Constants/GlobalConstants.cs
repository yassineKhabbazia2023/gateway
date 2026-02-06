using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace ApiGateway.Constants;

public static class GlobalsConstants
{
    public static readonly string[] NoAccountCheckPermissions = ["COOFF002", "CORAP001", "COADMI003"];
    public static readonly string[] NoRoleCheckPermissions = ["COADMI004"];
    public static readonly string[] NoAccountCheckEndpoints = ["ged-services", "/api/authorizations/configuration/account", "gtw/ventya/api"];
    public static readonly int CollaboratorAccountId = -1;
    public static readonly string AccountIdHeader = "Account-Id";
    public static readonly string cacheAccountId = "cache-accountId";
    public static readonly string cacheContactId = "cache-contactId";
    public static readonly string cacheContent = "cache-content";
    public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
    public static readonly string UserPermissionsHeader = "User-Permissions";
}