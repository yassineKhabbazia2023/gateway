namespace ApiGateway.Booking.Models;

public class BookingProvisioningRequest
{
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsOnline { get; set; }
    public int Duration { get; set; }
    public List<BookingServiceAvailabilityRequest> Availability { get; set; } = [];
}
