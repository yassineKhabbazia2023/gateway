using ApiGateway.Booking;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using System.Net;

namespace ApiGateway.DelegatingHandlers;

public class BookingWhitelistHandler(IBookingExperienceGuards bookingGuards, ILogger<BookingWhitelistHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = JwtHelper.ExtractBearerToken(request);
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("[Response]: 401 - [Handler]: BookingWhitelistHandler - [Function]: SendAsync - [Reason]: Missing or invalid bearer token");
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
            };
        }

        string userEmail;
        try
        {
            userEmail = JwtHelper.ExtractUserEmailFromToken(token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Response]: 401 - [Handler]: BookingWhitelistHandler - [Function]: SendAsync - [Reason]: Failed to extract email from token");
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
            };
        }

        if (bookingGuards.HasAccess(userEmail))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        logger.LogWarning("[Response]: 401 - [Handler]: BookingWhitelistHandler - [Function]: SendAsync - [Reason]: User {UserEmail} is not in XpBookingWhitelist", userEmail);
        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
        };
    }
}
