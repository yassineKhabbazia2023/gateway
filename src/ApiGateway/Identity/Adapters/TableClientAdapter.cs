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
            this.tableClient = CreateTableClient(storageUri, tableName);
        }

        public AsyncPageable<TableEntity> Query(string filter)
        {
            return this.tableClient.QueryAsync<TableEntity>(filter);
        }

        private TableClient CreateTableClient(string storageUri, string tableName)
        {
            var credential = new ManagedIdentityCredential("1fca77c6-2324-42b9-b34f-84aab45c9277");

            if (!storageUri.EndsWith("/"))
            {
                storageUri += "/";
            }

            var serviceClient = new TableServiceClient(new Uri(storageUri), credential);


            var tableClient = serviceClient.GetTableClient(tableName);

            return tableClient;
        }
    }
}
