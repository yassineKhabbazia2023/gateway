using ApiGateway.Booking;
using ApiGateway.Exceptions;
using System.Net;

namespace ApiGateway.DelegatingHandlers;

public class BookingFeatureFlagHandler(IBookingExperienceGuards bookingGuards, ILogger<BookingFeatureFlagHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (bookingGuards.IsFeatureFlagEnabled())
        {
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogWarning("[Response]: 403 - [Handler]: BookingFeatureFlagHandler - [Function]: SendAsync - [Reason]: Booking experience is disabled by feature flag");
        return new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingFeatureFlagDisabledCode, ErrorMessage = Errors.XpBookingFeatureFlagDisabledMessage })
        };
    }
}
