using Azure.Data.Tables;
using Azure;

namespace ApiGateway.Identity.Adapters
{
    public interface ITableClientAdapter
    {
        AsyncPageable<TableEntity> Query(string filter);
    }
}
