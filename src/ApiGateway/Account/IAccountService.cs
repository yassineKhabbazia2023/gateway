using ApiGateway.Models;
using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.Account;

public interface IAccountService
{
    public Task<Paging<Models.Account>> GetContactRolesAsync(int contactId);

    public Task<Models.Account?> GetAccountAsync(int accountId);

    Task<bool> CheckContactRoleAsync(int contactId, int? accountId, string? accountNumber);

    Task<bool> CheckContactsCommonAccountRole(int currentUserId, int contactId);

    Task<IReadOnlyCollection<FavoriteAccount>?> GetFavoriteAccountsByContactIdAsync(int contactId);

    Task<Summary?> GetSummaryAsync(int accountId, int currentUserId);

    Task<AccountCreated> CreateAccountForProspectAsync(CreateAccountRequest request, int? currentUserId, CancellationToken ct);

    Task<CreateRolesBulkResult> CreateRolesAsync(int accountId, IReadOnlyCollection<CreateRolesBulkItem> contacts, int? currentUserId, CancellationToken ct);

    /// <summary>
    /// Checks whether the contact exists and only has roles on prospect accounts.
    /// </summary>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The prospect-only check result.</returns>
    Task<ProspectOnlyContactResult> GetProspectOnlyContactResultAsync(int contactId, CancellationToken ct);
}
