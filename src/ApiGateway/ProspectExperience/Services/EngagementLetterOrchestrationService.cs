using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public class EngagementLetterOrchestrationService(
    IProspectApiClient prospectApiClient,
    IRegistryProspectClient registryProspectClient) : IEngagementLetterOrchestrationService
{
    public async Task<EngagementLetterOrchestrationOutcome> SendAsync(int prospectId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
    {
        var eligibility = await prospectApiClient.GetEngagementLetterEligibilityAsync(prospectId, currentUserId, ct);

        if (eligibility is null)
        {
            return EngagementLetterOrchestrationOutcome.ProspectNotFound;
        }

        if (eligibility.AlreadySent)
        {
            return EngagementLetterOrchestrationOutcome.AlreadySent;
        }

        if (!eligibility.CanSend)
        {
            return EngagementLetterOrchestrationOutcome.NotEligible;
        }

        var accountNumber = await prospectApiClient.GetAkuiteoAccountNumberByProspectIdAsync(prospectId, ct);

        if (string.IsNullOrEmpty(accountNumber))
        {
            return EngagementLetterOrchestrationOutcome.AccountNumberNotFound;
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, ct);
        var document = new ProspectDocumentContentResponse(
            Content: memoryStream.ToArray(),
            ContentType: file.ContentType,
            FileName: file.FileName);

        await registryProspectClient.UploadAkuiteoDocumentAsync(accountNumber, document, ct);
        await prospectApiClient.SendEngagementLetterAsync(prospectId, currentUserId, contactEmail, file, ct);

        return EngagementLetterOrchestrationOutcome.Sent;
    }
}
