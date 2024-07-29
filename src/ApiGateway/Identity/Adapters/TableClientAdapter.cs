using Azure;
using Azure.Data.Tables;

namespace ApiGateway.Identity.Adapters
{
    public class TableClientAdapter : ITableClientAdapter
    {
        private readonly TableClient tableClient;

        public TableClientAdapter(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            this.tableClient = CreateTableClient(storageUri, tableName,credentials);
        }

        public  AsyncPageable<TableEntity> Query(string filter)
        {
            return this.tableClient.QueryAsync<TableEntity>(filter);
        }

        private TableClient CreateTableClient(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            return new TableClient(
                new Uri(storageUri),
                tableName,
                credentials);
        }
    }
}
