using ApiGateway.Identity.Adapters;
using Azure.Data.Tables;

namespace ApiGateway.Identity.Factories
{
    public interface ITableClientFactory
    {
        ITableClientAdapter Create(string storageUri, string tableName, TableSharedKeyCredential credentials);
    }
}
