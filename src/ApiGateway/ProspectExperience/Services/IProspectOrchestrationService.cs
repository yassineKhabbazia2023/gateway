using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

public interface IProspectService
{
    Task<ProspectListItem> CreateAsync(CreateProspectRequest request, string? contactEmail, CancellationToken ct);

    /// <summary>
    /// Orchestrates the completion of a prospect onboarding step.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="request">The step completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The consolidated upload result.</returns>
    Task<DocumentUploadResultResponse> CompleteStepAsync(
        int accountId,
        CompleteStepRequest request,
        CancellationToken ct);

    /// <summary>
    /// Orchestrates the completion of a prospect onboarding step with completion audit metadata.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="request">The step completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <returns>The consolidated upload result.</returns>
    Task<DocumentUploadResultResponse> CompleteStepAsync(
        int accountId,
        CompleteStepRequest request,
        CancellationToken ct,
        int? currentUserId);

    /// <summary>
    /// Uploads a supporting document with automatic Akuiteo sync and step completion orchestration.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The current user identifier.</param>
    /// <param name="contactEmail">The uploader email address.</param>
    /// <param name="request">The upload request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task UploadSupportingDocumentAsync(
        int accountId,
        int prospectId,
        int currentUserId,
        string? contactEmail,
        UploadSupportingDocumentRequest request,
        CancellationToken ct);
}
