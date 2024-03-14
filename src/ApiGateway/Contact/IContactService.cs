namespace ApiGateway.Contact;

public interface IContactService
{
    Task<string?> GetContactAsync(string contactApiUri, string userEmail);
}
