using System.Net;

namespace ApiGateway.Booking.Models;

public class BookingApiResponse
{
    public HttpStatusCode StatusCode { get; init; }
    public bool IsSuccessStatusCode { get; init; }
    public string Content { get; init; } = string.Empty;
}
