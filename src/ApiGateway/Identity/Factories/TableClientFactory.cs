using ApiGateway.Identity.Adapters;
using Azure.Data.Tables;

namespace ApiGateway.Identity.Factories
{
    public class TableClientFactory : ITableClientFactory
    {
        public ITableClientAdapter Create(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            return new TableClientAdapter(storageUri, tableName, credentials);
        }


        public ITableClientAdapter Create(string storageUri, string tableName, string mangedIdentityClientId)
        {
            return new TableClientAdapter(storageUri, tableName, mangedIdentityClientId);
        }
    }
}
