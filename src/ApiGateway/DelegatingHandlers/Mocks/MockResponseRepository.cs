using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ApiGateway.DelegatingHandlers.Mocks;

/// <summary>
/// Reads and writes the mocked responses stored as blobs.
///
/// The blob client is resolved lazily: <see cref="MockResponseHandler"/> is registered
/// globally and therefore built for every route, while the feature flag is only checked at
/// execution time. Without that laziness, a reachable storage would be required even with
/// mocks disabled.
/// </summary>
public class MockResponseRepository(
    Lazy<BlobContainerClient> containerClient) : IMockResponseRepository
{
    private BlobContainerClient Container => containerClient.Value;

    public async Task<(bool Success, string? JsonContent)> GetJsonContentAsync(string routeKey)
    {
        var blobClient = Container.GetBlobClient(ToSafeBlobName(routeKey));
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
        var blobClient = Container.GetBlobClient(ToSafeBlobName(routeKey));
        await blobClient.UploadAsync(BinaryData.FromString(jsonContent), overwrite: true);
    }

    public async Task<bool> DeleteAsync(string routeKey)
    {
        var blobClient = Container.GetBlobClient(ToSafeBlobName(routeKey));
        var response = await blobClient.DeleteIfExistsAsync();
        return response.Value;
    }

    public async Task<IReadOnlyList<MockEntry>> ListAllAsync()
    {
        var entries = new List<MockEntry>();

        await foreach (var blobItem in Container.GetBlobsAsync())
        {
            entries.Add(await ReadEntryAsync(blobItem));
        }

        return entries;
    }

    /// <summary>
    /// Enumerating the container only yields metadata: the content of each mock requires a
    /// separate download.
    /// </summary>
    private async Task<MockEntry> ReadEntryAsync(BlobItem blobItem)
    {
        var content = await Container.GetBlobClient(blobItem.Name).DownloadContentAsync(default);

        return new MockEntry(FromBlobName(blobItem.Name), content.Value.Content.ToString());
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
