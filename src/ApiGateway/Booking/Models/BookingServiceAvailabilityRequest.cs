namespace ApiGateway.Booking.Models;

public class BookingServiceAvailabilityRequest
{
    public string Day { get; set; } = string.Empty;
    public List<BookingServiceTimeSlotRequest> TimeSlots { get; set; } = [];
}
