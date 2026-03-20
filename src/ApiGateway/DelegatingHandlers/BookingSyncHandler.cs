using ApiGateway.Booking;

namespace ApiGateway.DelegatingHandlers;

public class BookingSyncHandler(IBookingSyncTrigger syncTrigger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (TryExtractContactId(request, out var contactId) && syncTrigger.ShouldTriggerSync(contactId))
        {
            syncTrigger.TriggerSync(contactId);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool TryExtractContactId(HttpRequestMessage request, out int contactId)
    {
        contactId = 0;
        if (!request.Headers.TryGetValues("CurrentUser", out var values))
        {
            return false;
        }

        return int.TryParse(values.FirstOrDefault(), out contactId) && contactId > 0;
    }
}
