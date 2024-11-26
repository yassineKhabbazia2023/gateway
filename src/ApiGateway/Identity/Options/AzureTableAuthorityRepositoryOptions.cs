using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Options
{
    [ExcludeFromCodeCoverage]
    public class AzureTableAuthorityRepositoryOptions
    {
        public string IsvcAzureStorageName { get; set; }
        public string IsvcAzureStorageUri { get; set; }
        public string IsvcAzureStorageKey { get; set; }
        public string ManagedIdentityClientId  { get; set; }
    }
}
