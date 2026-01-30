namespace ApiGateway.Contact;

public interface IContactService
{
    Task<string?> GetContactIdAsync(string userEmail);
    Task<Models.Contact?> GetContactAsync(string userEmail);
    Task<Models.Contact?> GetContactByIdAsync(int contactId);
}
