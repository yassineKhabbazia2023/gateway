using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public interface IEngagementLetterOrchestrationService
{
    /// <summary>
    /// Orchestrates the engagement letter flow:
    /// checks eligibility in Prospect, uploads the file to Registry (Akuiteo),
    /// then records the engagement letter in Prospect.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="currentUserId">The collaborator identifier.</param>
    /// <param name="contactEmail">The collaborator email address.</param>
    /// <param name="file">The PDF file to send.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<EngagementLetterOrchestrationOutcome> SendAsync(int accountId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct);
}
