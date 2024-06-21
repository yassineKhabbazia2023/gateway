using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace ApiGateway.Constants;

public static class GlobalsConstants
{
    public static readonly string[] NoAccountCheckPermissions = ["COOFF002", "CORAP001", "COADMI003"];
    public static readonly int CollaboratorAccountId = -1;
    public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
}