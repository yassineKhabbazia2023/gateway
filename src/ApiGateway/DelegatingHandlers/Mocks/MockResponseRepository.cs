using Azure;
using Azure.Storage.Blobs;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseRepository(
    BlobContainerClient containerClient) : IMockResponseRepository
{
    public async Task<(bool Success, string? JsonContent)> GetJsonContentAsync(string routeKey)
    {
        var blobClient = containerClient.GetBlobClient(ToSafeBlobName(routeKey));
        try
        {
            var response = await blobClient.DownloadContentAsync(default);
            return (true, response.Value.Content.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return (false, null);
        }
    }

    public async Task UpsertAsync(string routeKey, string jsonContent)
    {
        var blobClient = containerClient.GetBlobClient(ToSafeBlobName(routeKey));
        await blobClient.UploadAsync(BinaryData.FromString(jsonContent), overwrite: true);
    }

    public async Task<bool> DeleteAsync(string routeKey)
    {
        var blobClient = containerClient.GetBlobClient(ToSafeBlobName(routeKey));
        var response = await blobClient.DeleteIfExistsAsync();
        return response.Value;
    }

    public async Task<IReadOnlyList<MockEntry>> ListAllAsync()
    {
        var entries = new List<MockEntry>();
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            var blobClient = containerClient.GetBlobClient(blobItem.Name);
            var content = await blobClient.DownloadContentAsync(default);
            entries.Add(new MockEntry(
                FromBlobName(blobItem.Name),
                content.Value.Content.ToString()));
        }
        return entries;
    }

    public static string ToSafeBlobName(string routeKey)
    {
        return routeKey
            .Replace("/", "__")
            .Replace(":", "--")
            + ".json";
    }

    public static string FromBlobName(string blobName)
    {
        return blobName
            .Replace(".json", "")
            .Replace("--", ":")
            .Replace("__", "/");
    }
}
