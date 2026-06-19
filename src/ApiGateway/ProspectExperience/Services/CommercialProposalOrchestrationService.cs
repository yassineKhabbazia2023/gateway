using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

public class CommercialProposalOrchestrationService(
    IProspectApiClient prospectApiClient,
    IRegistryProspectClient registryProspectClient) : ICommercialProposalOrchestrationService
{
    public async Task<CommercialProposalOrchestrationOutcome> SendAsync(int prospectId, int currentUserId, IFormFile file, CancellationToken ct)
    {
        var eligibility = await prospectApiClient.GetCommercialProposalEligibilityAsync(prospectId, currentUserId, ct);

        if (eligibility is null)
        {
            return CommercialProposalOrchestrationOutcome.ProspectNotFound;
        }

        if (eligibility.AlreadySent)
        {
            return CommercialProposalOrchestrationOutcome.AlreadySent;
        }

        if (!eligibility.CanSend)
        {
            return CommercialProposalOrchestrationOutcome.NotEligible;
        }

        var accountNumber = await prospectApiClient.GetAkuiteoAccountNumberByProspectIdAsync(prospectId, ct);

        if (string.IsNullOrEmpty(accountNumber))
        {
            return CommercialProposalOrchestrationOutcome.AccountNumberNotFound;
        }
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, ct);
        var document = new ProspectDocumentContentResponse(
            Content: memoryStream.ToArray(),
            ContentType: file.ContentType,
            FileName: file.FileName);

        await registryProspectClient.UploadAkuiteoDocumentAsync(accountNumber, document, ct);
        await prospectApiClient.SendCommercialProposalAsync(prospectId, currentUserId, file, ct);

        return CommercialProposalOrchestrationOutcome.Sent;
    }

}
