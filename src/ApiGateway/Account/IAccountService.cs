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

    Task<Summary?> GetSummaryAsync(int accountId, int currentUserId, string contactType);

    Task<AccountCreated> CreateAccountForProspectAsync(CreateAccountRequest request, int? currentUserId, CancellationToken ct);

    Task<CreateRolesBulkResult> CreateRolesAsync(int accountId, IReadOnlyCollection<CreateRolesBulkItem> contacts, int? currentUserId, CancellationToken ct);

    /// <summary>
    /// Checks whether the contact exists and only has roles on prospect accounts.
    /// </summary>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The prospect-only check result.</returns>
    Task<ProspectOnlyContactResult> GetProspectOnlyContactResultAsync(int contactId, CancellationToken ct);

    Task UpdateLastActivityDateAsync(int currentUserId, string contactType, int accountId);

    /// <summary>
    /// Gets the Serenity eligibility state of a contact: whether a choice was already made, and
    /// otherwise the portfolio entities matching the Account-owned criteria.
    /// </summary>
    /// <param name="contactId">The contact identifier, resolved from the token.</param>
    /// <returns>The eligibility state, or <c>null</c> when the Account service is unreachable.</returns>
    Task<SerenityEligibility?> GetSerenityEligibilityAsync(int contactId);

    /// <summary>
    /// Persists the contact's Serenity choice. The choice is immutable.
    /// </summary>
    /// <param name="contactId">The contact identifier, resolved from the token.</param>
    /// <param name="isAccepted">The expressed choice.</param>
    /// <returns>A task.</returns>
    Task SetSerenityChoiceAsync(int contactId, bool isAccepted);
}
