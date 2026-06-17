using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Services;

public interface IProspectApiClient
{
    /// <summary>
    /// Retrieves the INPI company information used to initialize prospect creation.
    /// </summary>
    /// <param name="siret">The target SIRET.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The company information.</returns>
    Task<InpiCompanyInfo> GetInpiCompanyInfoAsync(string siret, CancellationToken ct);

    /// <summary>
    /// Retrieves the active incomplete prospect state for the provided SIRET when Gateway can resume a partial orchestration.
    /// </summary>
    /// <param name="siret">The target SIRET.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The incomplete prospect state, or null when no resumable state exists.</returns>
    Task<IncompleteProspectCreationState?> GetIncompleteProspectBySiretAsync(string siret, CancellationToken ct);

    /// <summary>
    /// Creates the prospect aggregate in Prospect.
    /// </summary>
    /// <param name="request">The gateway creation request.</param>
    /// <param name="inpi">The INPI company information.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created prospect identifier.</returns>
    Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, CancellationToken ct);

    /// <summary>
    /// Finalizes the prospect once synchronized account and contact rows are available in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="accountNumber">The account number.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="contactId">The signatory contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The finalization outcome returned by Prospect.</returns>
    Task<FinalizeProspectOutcome> UpdateProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, CancellationToken ct);

    /// <summary>
    /// Prepares an incomplete prospect for checkpoint-based resume by restoring a finalizable lifecycle state when needed.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The audit user identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Prospect acknowledged the resume preparation; otherwise false.</returns>
    Task<bool> PrepareCreationResumeAsync(int prospectId, int currentUserId, CancellationToken ct);

    /// <summary>
    /// Confirms whether the expected role rows linked to a finalized prospect have been synchronized locally in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The synchronization outcome returned by Prospect.</returns>
    Task<ProspectRoleSynchronizationOutcome> GetCreationRoleSynchronizationOutcomeAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Requests Prospect to retrieve and persist the active INPI beneficiaries.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Prospect synchronized the beneficiaries; otherwise false when the prospect was not found.</returns>
    Task<bool> PersistBeneficiariesAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Persists the current Gateway creation progress checkpoint in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The audit user identifier.</param>
    /// <param name="request">The technical progress payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when Prospect acknowledged the progress update; otherwise false.</returns>
    Task<bool> UpdateCreationProgressAsync(int prospectId, int currentUserId, UpdateProspectCreationProgressRequest request, CancellationToken ct);

    /// <summary>
    /// Marks the prospect creation lifecycle as failed in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The audit user identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="request">The latest known checkpoint data to keep resumable after the failure.</param>
    /// <returns>True when Prospect acknowledged the failed status update; otherwise false.</returns>
    Task<bool> MarkProspectCreationFailedAsync(int prospectId, int currentUserId, MarkProspectCreationFailedRequest request, CancellationToken ct);

    /// <summary>
    /// Gets the prospect identifier for a given account identifier.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The prospect identifier, or null if no active prospect is linked to this account.</returns>
    Task<int?> GetProspectIdByAccountIdAsync(int accountId, CancellationToken ct);
}
