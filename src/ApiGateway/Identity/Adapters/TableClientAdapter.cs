using Azure;
using Azure.Data.Tables;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Adapters
{
    [ExcludeFromCodeCoverage]
    public class TableClientAdapter : ITableClientAdapter
    {
        private readonly TableClient tableClient;

        public TableClientAdapter(string storageUri, string tableName, TableSharedKeyCredential credentials)
        {
            this.tableClient = CreateTableClient(storageUri, tableName, credentials);
        }

        public AsyncPageable<TableEntity> Query(string filter)
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
