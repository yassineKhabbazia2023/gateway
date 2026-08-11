using ApiGateway.Booking.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiGateway.Booking;

public class BookingSyncTrigger(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<XpBookingOptions> bookingOptions,
    ILogger<BookingSyncTrigger> logger) : IBookingSyncTrigger
{
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(bookingOptions.Value.SyncIntervalMinutes);
    private const string CacheKeyPrefix = "booking_sync_";

    public bool ShouldTriggerSync(int contactId)
    {
        var cacheKey = $"{CacheKeyPrefix}{contactId}";

        if (memoryCache.TryGetValue(cacheKey, out _))
        {
            return false;
        }

        memoryCache.Set(cacheKey, true, _syncInterval);
        return true;
    }

    public void TriggerSync(int contactId)
    {
        _ = TriggerSyncAsync(contactId);
    }

    private async Task TriggerSyncAsync(int contactId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("BookingClient");
            var syncRequest = new HttpRequestMessage(HttpMethod.Post, "api/booking/sync");
            syncRequest.Headers.Add("CurrentUser", contactId.ToString());
            await client.SendAsync(syncRequest);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to trigger booking sync for contact {ContactId}.", contactId);
        }
    }
}
