using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public interface ICommercialProposalOrchestrationService
{
    /// <summary>
    /// Orchestrates the commercial proposal flow:
    /// checks eligibility in Prospect, uploads the file to Registry (Akuiteo),
    /// then records the proposal in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="contactEmail">The collaborator email address.</param>
    /// <param name="file">The PDF file to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// True when the proposal was sent.
    /// False when the prospect does not exist, is archived, or is not yet linked to an Akuiteo account.
    /// Throws when the prospect is not eligible (already sent or user is not a collaborator).
    /// </returns>
    Task<CommercialProposalOrchestrationOutcome> SendAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct);

}
