using ApiGateway.Identity.Adapters;
using Azure.Data.Tables;

namespace ApiGateway.Identity.Factories
{
    public interface ITableClientFactory
    {
        /// <summary>
        /// Create a table storage client while connecting via storage key
        /// </summary>
        /// <param name="storageUri"></param>
        /// <param name="tableName"></param>
        /// <param name="credentials"></param>
        /// <returns></returns>
        ITableClientAdapter Create(string storageUri, string tableName, TableSharedKeyCredential credentials);


        /// <summary>
        /// Create a table storage client while connecting via managed identity
        /// </summary>
        /// <param name="storageUri"></param>
        /// <param name="tableName"></param>
        /// <param name="mangedIdentityClientId"></param>
        /// <returns></returns>
        ITableClientAdapter Create(string storageUri, string tableName, string mangedIdentityClientId);
    }
}
