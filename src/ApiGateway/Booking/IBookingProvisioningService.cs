using ApiGateway.Booking.Models;

namespace ApiGateway.Booking;

public interface IBookingProvisioningService
{
    Task<BookingApiResponse> CreateBusinessAsync(int contactId, string bearerToken);
    Task<BookingApiResponse> CreateServiceAsync(string bookingBusinessId, int contactId,
        BookingProvisioningRequest request, string bearerToken);
    Task<BookingApiResponse> DeleteBusinessAsync(string businessEmail, int contactId, string bearerToken);
}
