namespace ApiGateway.Cache;

public interface ICacheService
{
    Task<Contact.Models.Contact?> GetAsync(string key);
    Task SetContactAsync(string userEmail, Contact.Models.Contact contact);
}
