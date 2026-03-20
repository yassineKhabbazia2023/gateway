namespace ApiGateway.Booking;

public interface IBookingSyncTrigger
{
    bool ShouldTriggerSync(int contactId);
    void TriggerSync(int contactId);
}
