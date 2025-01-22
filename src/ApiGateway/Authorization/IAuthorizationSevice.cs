namespace ApiGateway.Authorization;

public interface IAuthorizationSevice
{
    Task<IList<string>> GetContactAuthorizationAsync(int contactId, int? accountId);
}
