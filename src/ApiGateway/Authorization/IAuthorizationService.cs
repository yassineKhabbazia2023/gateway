namespace ApiGateway.Authorization;

public interface IAuthorizationService
{
    Task<List<string>> GetContactAuthorizationAsync(int contactId, int? accountId);

    Task<List<string>> GetAllContactAuthorizationAsync(int contactId, int? accountId);

    Task<List<string>> GetAllContactAuthorizationsAsync(int contactId);

    Task<bool> CreateOrUpdateContactAccountAuthorizationAsync(int contactId, int accountId, IList<string> codes);

}
