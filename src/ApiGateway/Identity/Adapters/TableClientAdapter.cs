using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Adapters
{
    [ExcludeFromCodeCoverage]
    public class TableClientAdapter : ITableClientAdapter
    {
        private readonly TableClient tableClient;

        public TableClientAdapter(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            this.tableClient = CreateTableClientBySharedKeyCredential(storageUri, tableName, credentials);
        }

        public TableClientAdapter(string storageUri, string tableName, string mangedIdentityClientId)
        {
            this.tableClient = CreateTableClientByManagedIdentity(storageUri, tableName, mangedIdentityClientId);
        }

        public AsyncPageable<TableEntity> Query(string filter)
        {
            return this.tableClient.QueryAsync<TableEntity>(filter);
        }

        #region Table service client creators
        private TableServiceClient CreateTableServiceClientByManagedIdentityId(string storageUri, string managedIdentityClientId)
        {
            var credential = new ManagedIdentityCredential(managedIdentityClientId);
            var client = new TableServiceClient(new Uri(storageUri), credential);
            return client;
        }
        #endregion

        #region Table client creators
        private TableClient CreateTableClientByManagedIdentity(string storageUri, string tableName, string managedIdentityClientId)
        {
            if (!storageUri.EndsWith("/"))
            {
                storageUri += "/";
            }

            TableServiceClient serviceClient = CreateTableServiceClientByManagedIdentityId(storageUri, managedIdentityClientId);


            var tableClient = serviceClient.GetTableClient(tableName);

            return tableClient;
        }

        private TableClient CreateTableClientBySharedKeyCredential(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            return new TableClient(
                new Uri(storageUri),
                tableName,
                credentials);
        }
        #endregion
    }
}
