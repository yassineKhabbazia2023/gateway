using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;

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
    /// Retrieves the documents that still need to be uploaded to an external service for an onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The upload plan, or null when the prospect does not exist.</returns>
    Task<DocumentsToUploadToExternalServiceResponse?> GetDocumentsToUploadToExternalServiceAsync(
        int prospectId,
        string stepName,
        CancellationToken ct);

    /// <summary>
    /// Downloads one beneficiary identity document together with the metadata required for downstream upload.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The document content and metadata, or null when the document does not exist.</returns>
    Task<ProspectDocumentContentResponse?> GetDocumentAsync(int prospectId, int documentId, CancellationToken ct);

    /// <summary>
    /// Uploads a prospect document through the Prospect document endpoint.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The current user identifier.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The uploaded document identifier, or null when the prospect is not found.</returns>
    Task<int?> UploadDocumentAsync(
        int prospectId,
        int currentUserId,
        string documentType,
        IFormFile file,
        CancellationToken ct);

    /// <summary>
    /// Persists the consolidated document upload result in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The upload result payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The consolidated upload result, or null when the prospect does not exist.</returns>
    Task<DocumentUploadResultResponse?> RegisterDocumentUploadResultAsync(
        int prospectId,
        DocumentUploadResultRequest request,
        CancellationToken ct);

    /// <summary>
    /// Gets the prospect account information needed by Gateway onboarding flows.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The prospect account information, or null when not found.</returns>
    Task<ProspectAccountResponse?> GetProspectAccountAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Determines whether a contact is a signatory for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="contactId">The contact identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when the contact is a prospect signatory; otherwise false.</returns>
    Task<bool> IsProspectSignatoryAsync(int prospectId, int contactId, CancellationToken ct);

    /// <summary>
    /// Completes a named Prospect onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompleteStepAsync(int prospectId, string stepName, CancellationToken ct);

    /// <summary>
    /// Completes a named Prospect onboarding step with completion audit metadata.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The optional current collaborator contact identifier.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompleteStepAsync(int prospectId, string stepName, CancellationToken ct, int? currentUserId);

    /// <summary>
    /// Resets a named Prospect onboarding step to TODO.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The public onboarding step name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetStepAsync(int prospectId, string stepName, CancellationToken ct);

    /// <summary>
    /// Marks the Prospect payment method onboarding step as in progress.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkPaymentMethodInProgressAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Requests Prospect to send payment preference notification emails.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="signatoryEmail">The signatory email address.</param>
    /// <param name="collabReceiversEmails">The collaborator or BS receiver email addresses.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendPaymentPreferenceNotificationsAsync(
        int prospectId,
        string signatoryEmail,
        string[] collabReceiversEmails,
        CancellationToken ct);

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

    /// <summary>
    /// Gets the account identifier for a given prospect identifier.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The account identifier, or null if the prospect does not exist, is archived, or has no account.</returns>
    Task<int?> GetAccountIdByProspectIdAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Gets the Akuiteo account number for a given active prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Akuiteo account number, or null when the prospect is not found or has no account number yet.</returns>
    Task<string?> GetAkuiteoAccountNumberByProspectIdAsync(int prospectId, CancellationToken ct);

    /// <summary>
    /// Checks whether the given user is allowed to send a commercial proposal for the prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The eligibility response, or null when the prospect does not exist or is archived.</returns>
    Task<CommercialProposalEligibilityResponse?> GetCommercialProposalEligibilityAsync(int prospectId, int currentUserId, CancellationToken ct);

    /// <summary>
    /// Sends the commercial proposal PDF for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="contactEmail">The collaborator email address.</param>
    /// <param name="file">The PDF file.</param>
    /// <param name="ct">The cancellation token.</param>
    Task SendCommercialProposalAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct);

    /// <summary>
    /// Checks whether the given user is allowed to send an engagement letter for the prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The eligibility response, or null when the prospect does not exist or is archived.</returns>
    Task<EngagementLetterEligibilityResponse?> GetEngagementLetterEligibilityAsync(int prospectId, int currentUserId, CancellationToken ct);

    /// <summary>
    /// Sends the engagement letter PDF for a prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="contactEmail">The collaborator email address.</param>
    /// <param name="file">The PDF file.</param>
    /// <param name="ct">The cancellation token.</param>
    Task SendEngagementLetterAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct);

    /// <summary>
    /// Uploads a supporting document to Prospect storage.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The current user identifier.</param>
    /// <param name="request">The upload request with document type and file.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The upload result with document ID or error details.</returns>
    Task<UploadSupportingDocumentResult> UploadSupportingDocumentAsync(
        int prospectId,
        int currentUserId,
        UploadSupportingDocumentRequest request,
        CancellationToken ct);

    /// <summary>
    /// Gets the document requirements for a prospect based on legal form.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The document requirements with upload status, or null if prospect not found.</returns>
    Task<DocumentRequirementsResponse?> GetDocumentRequirementsAsync(
        int prospectId,
        CancellationToken ct);
}
