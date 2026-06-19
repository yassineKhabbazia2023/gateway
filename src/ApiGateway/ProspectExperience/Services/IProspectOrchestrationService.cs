using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

public interface IProspectService
{
    Task<ProspectListItem> CreateAsync(CreateProspectRequest request, CancellationToken ct);

    /// <summary>
    /// Orchestrates the completion of a prospect onboarding step.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The step completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <returns>The consolidated upload result.</returns>
    Task<DocumentUploadResultResponse> CompleteStepAsync(
        int prospectId,
        CompleteStepRequest request,
        CancellationToken ct);

    /// <summary>
    /// Orchestrates the completion of a prospect onboarding step with completion audit metadata.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The step completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="currentUserId">The current collaborator contact identifier.</param>
    /// <returns>The consolidated upload result.</returns>
    Task<DocumentUploadResultResponse> CompleteStepAsync(
        int prospectId,
        CompleteStepRequest request,
        CancellationToken ct,
        int? currentUserId);
}
