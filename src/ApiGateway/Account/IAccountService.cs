using ApiGateway.Models;

namespace ApiGateway.Account;

public interface IAccountService
{
    public Task<Paging<Models.Account>> GetContactRolesAsync(int contactId);
}