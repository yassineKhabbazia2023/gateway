using Azure;
using Azure.Data.Tables;
using Azure.Identity;

namespace ApiGateway.Identity.Adapters
{
    public class TableClientAdapter : ITableClientAdapter
    {
        private readonly TableClient tableClient;

        public TableClientAdapter(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            this.tableClient = CreateTableClient(storageUri, tableName);
        }

        public  AsyncPageable<TableEntity> Query(string filter)
        {
            return this.tableClient.QueryAsync<TableEntity>(filter);
        }

        private TableClient CreateTableClient(string storageUri, string tableName)
        {
            var credential = new ManagedIdentityCredential();

            if (!storageUri.EndsWith("/"))
            {
                storageUri += "/";
            }
           
            var serviceClient = new TableServiceClient(new Uri(storageUri), credential);

            
            var tableClient = serviceClient.GetTableClient(tableName);

            return tableClient;
        }

        #region only to test api in local env
        /**
         * To test the API locally:
         * 1. Comment out the CreateTableClient method that uses the managed identity.
         * 2. Uncomment this method.
         * 3. Ensure the following environment variables are set:
         *    - IsvcAzureStorageKey
         *    - IsvcAzureStorageUri
         *    - IsvcAzureStorageName
         **/

        //private TableClient CreateTableClient(string storageUri, string tableName, TableSharedKeyCredential credentials)
        //{
        //    return new TableClient(
        //        new Uri(storageUri),
        //        tableName,
        //        credentials);
        //}
        #endregion
    }
}
