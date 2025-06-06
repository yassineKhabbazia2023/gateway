namespace ApiGateway.Authorization;

public interface IAuthorizationSevice
{
    Task<List<string>> GetContactAuthorizationAsync(int contactId, int? accountId);

    Task<List<string>> GetAllContactAuthorizationAsync(int contactId, int? accountId);

    Task<List<string>> GetAllContactAuthorizationsAsync(int contactId);

}
