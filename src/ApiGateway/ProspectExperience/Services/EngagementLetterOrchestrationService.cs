using ApiGateway.Account;
using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public class EngagementLetterOrchestrationService(
    IProspectApiClient prospectApiClient,
    IRegistryProspectClient registryProspectClient,
    IAccountService accountService) : IEngagementLetterOrchestrationService
{
    public async Task<EngagementLetterOrchestrationOutcome> SendAsync(int accountId, int currentUserId, string? contactEmail, IFormFile file, CancellationToken ct)
    {
        var eligibility = await prospectApiClient.GetEngagementLetterEligibilityAsync(accountId, currentUserId, ct);

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

        var account = await accountService.GetAccountAsync(accountId);
        var accountNumber = account?.AccountNumber;

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
        await prospectApiClient.SendEngagementLetterAsync(accountId, currentUserId, contactEmail, file, ct);
        return EngagementLetterOrchestrationOutcome.Sent;
    }
}
