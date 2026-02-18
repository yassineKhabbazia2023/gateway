namespace ApiGateway.Booking;

public interface IBookingExperienceGuards
{
    bool HasAccess(string email);
    bool IsFeatureFlagEnabled();
}
