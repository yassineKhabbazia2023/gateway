using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Uploads complementary supporting documents to Akuitéo independently from the supporting documents step.
/// </summary>
public sealed class AdditionalSupportingDocumentUploadStrategy(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    ILogger<AdditionalSupportingDocumentUploadStrategy> logger) : IAdditionalSupportingDocumentUploadStrategy
{
    private const string SupportingDocumentsStepName = "SupportingDocuments";

    /// <inheritdoc />
    public async Task<DocumentExternalUploadBatchResult> UploadAsync(int prospectId, int documentId, CancellationToken ct)
    {
        var accountNumber = await prospectClient.GetAkuiteoAccountNumberByProspectIdAsync(prospectId, ct);
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new GatewayException(
                StatusCodes.Status400BadRequest,
                Errors.NullArgumentCode,
                "The prospect Akuitéo account number is required to upload a complementary supporting document.");
        }

        var document = await prospectClient.GetDocumentAsync(prospectId, documentId, ct);
        if (document is null)
        {
            logger.LogWarning(
                "Complementary supporting document {DocumentId} could not be downloaded for prospect {ProspectId}",
                documentId,
                prospectId);

            await PersistUploadResultAsync(prospectId, [], [documentId], ct);
            return new DocumentExternalUploadBatchResult([], [documentId]);
        }

        var uploaded = await registryClient.UploadAkuiteoDocumentAsync(accountNumber, document, ct);
        if (uploaded)
        {
            await PersistUploadResultAsync(prospectId, [documentId], [], ct);

            logger.LogInformation(
                "Complementary supporting document {DocumentId} uploaded to Akuitéo for prospect {ProspectId}",
                documentId,
                prospectId);

            return new DocumentExternalUploadBatchResult([documentId], []);
        }

        logger.LogWarning(
            "Complementary supporting document upload to Akuitéo failed. ProspectId: {ProspectId}, DocumentId: {DocumentId}, AccountNumber: {AccountNumber}, FileName: {FileName}",
            prospectId,
            documentId,
            accountNumber,
            document.FileName);

        await PersistUploadResultAsync(prospectId, [], [documentId], ct);
        return new DocumentExternalUploadBatchResult([], [documentId]);
    }

    /// <summary>
    /// Persists the complementary supporting document upload result in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="succeededDocumentIds">The successfully uploaded document identifiers.</param>
    /// <param name="failedDocumentIds">The failed document identifiers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PersistUploadResultAsync(
        int prospectId,
        IReadOnlyList<int> succeededDocumentIds,
        IReadOnlyList<int> failedDocumentIds,
        CancellationToken ct)
    {
        var persistedUploadResult = await prospectClient.RegisterDocumentUploadResultAsync(
            prospectId,
            new DocumentUploadResultRequest(
                SupportingDocumentsStepName,
                succeededDocumentIds,
                failedDocumentIds),
            ct);
        if (persistedUploadResult is not null)
        {
            return;
        }

        logger.LogWarning(
            "Complementary supporting document upload result could not be persisted for prospect {ProspectId}",
            prospectId);

        throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, "Prospect was not found.");
    }
}
