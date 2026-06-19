using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Completes the beneficiary onboarding step by uploading pending documents to Akuitéo.
/// </summary>
public sealed class BeneficiaryStepCompletionStrategy(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    ILogger<BeneficiaryStepCompletionStrategy> logger) : IProspectStepCompletionStrategy
{
    private const string BeneficiaryStepName = "Beneficiary";

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanHandle(string stepName)
    {
        return string.Equals(BeneficiaryStepName, stepName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct)
    {
        return await CompleteAsync(
            prospectId,
            stepName,
            ct,
            completeStepAsync: () => prospectClient.CompleteStepAsync(prospectId, stepName, ct));
    }

    /// <inheritdoc />
    public async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct,
        int? currentUserId)
    {
        return await CompleteAsync(
            prospectId,
            stepName,
            ct,
            completeStepAsync: () => prospectClient.CompleteStepAsync(prospectId, stepName, ct, currentUserId));
    }

    private async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct,
        Func<Task> completeStepAsync)
    {
        logger.LogInformation(
            "Starting beneficiary onboarding step completion for prospect {ProspectId}",
            prospectId);

        var uploadPlan = await prospectClient.GetDocumentsToUploadToExternalServiceAsync(
            prospectId,
            stepName,
            ct);
        if (uploadPlan is null)
        {
            logger.LogWarning(
                "Beneficiary onboarding step completion stopped because Prospect could not return a document upload plan for prospect {ProspectId} and step {StepName}",
                prospectId,
                stepName);
            throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, "Prospect or onboarding step was not found.");
        }

        if (uploadPlan.DocumentIds.Count == 0)
        {
            logger.LogInformation(
                "Beneficiary onboarding step completion stopped without external upload for prospect {ProspectId} and step {StepName}. Reason: {UploadPlanStatus}",
                prospectId,
                stepName,
                uploadPlan.Status);
            return new DocumentExternalUploadBatchResult([], []);
        }

        if (string.IsNullOrWhiteSpace(uploadPlan.AkuiteoAccountNumber))
        {
            throw new GatewayException(
                StatusCodes.Status400BadRequest,
                Errors.NullArgumentCode,
                "The prospect Akuitéo account number is required to upload onboarding step documents.");
        }

        var uploadResult = await UploadPendingDocumentsAsync(
            prospectId,
            stepName,
            uploadPlan.AkuiteoAccountNumber,
            uploadPlan.DocumentIds,
            ct);

        if (uploadResult.FailedDocumentIds.Count > 0)
        {
            await PersistUploadResultAsync(
                prospectId,
                stepName,
                [],
                uploadResult.FailedDocumentIds,
                ct);
        }

        if (uploadResult.FailedDocumentIds.Count == 0)
        {
            await completeStepAsync();
        }

        return uploadResult;
    }

    /// <summary>
    /// Uploads the pending documents sequentially and records each document outcome.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The onboarding step name.</param>
    /// <param name="akuiteoAccountNumber">The Akuitéo account number.</param>
    /// <param name="documentIds">The document identifiers to upload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The external upload batch result.</returns>
    private async Task<DocumentExternalUploadBatchResult> UploadPendingDocumentsAsync(
        int prospectId,
        string stepName,
        string akuiteoAccountNumber,
        IReadOnlyList<int> documentIds,
        CancellationToken ct)
    {
        var succeededDocumentIds = new List<int>();
        var failedDocumentIds = new List<int>();

        foreach (var documentId in documentIds)
        {
            var document = await prospectClient.GetDocumentAsync(prospectId, documentId, ct);
            if (document is null)
            {
                logger.LogWarning(
                    "Document {DocumentId} could not be downloaded for prospect {ProspectId}",
                    documentId,
                    prospectId);
                failedDocumentIds.Add(documentId);
                continue;
            }

            logger.LogInformation(
                "Uploading onboarding document to Registry for Akuitéo. ProspectId: {ProspectId}, StepName: {StepName}, DocumentId: {DocumentId}, AccountNumber: {AccountNumber}, FileName: {FileName}, ContentType: {ContentType}",
                prospectId,
                stepName,
                documentId,
                akuiteoAccountNumber,
                document.FileName,
                document.ContentType);

            var uploaded = await registryClient.UploadAkuiteoDocumentAsync(
                akuiteoAccountNumber,
                document,
                ct);
            if (uploaded)
            {
                await PersistUploadResultAsync(
                    prospectId,
                    stepName,
                    [documentId],
                    [],
                    ct);

                logger.LogInformation(
                    "Onboarding document uploaded to Registry for Akuitéo. ProspectId: {ProspectId}, StepName: {StepName}, DocumentId: {DocumentId}, AccountNumber: {AccountNumber}, FileName: {FileName}",
                    prospectId,
                    stepName,
                    documentId,
                    akuiteoAccountNumber,
                    document.FileName);
                succeededDocumentIds.Add(documentId);
                continue;
            }

            logger.LogWarning(
                "Onboarding document upload to Registry for Akuitéo failed. ProspectId: {ProspectId}, StepName: {StepName}, DocumentId: {DocumentId}, AccountNumber: {AccountNumber}, FileName: {FileName}",
                prospectId,
                stepName,
                documentId,
                akuiteoAccountNumber,
                document.FileName);
            failedDocumentIds.Add(documentId);
        }

        return new DocumentExternalUploadBatchResult(
            succeededDocumentIds,
            failedDocumentIds);
    }

    /// <summary>
    /// Persists the external upload result in Prospect.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The onboarding step name.</param>
    /// <param name="succeededDocumentIds">The successfully uploaded document identifiers.</param>
    /// <param name="failedDocumentIds">The failed document identifiers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PersistUploadResultAsync(
        int prospectId,
        string stepName,
        IReadOnlyList<int> succeededDocumentIds,
        IReadOnlyList<int> failedDocumentIds,
        CancellationToken ct)
    {
        var persistedUploadResult = await prospectClient.RegisterDocumentUploadResultAsync(
            prospectId,
            new DocumentUploadResultRequest(
                stepName,
                succeededDocumentIds,
                failedDocumentIds),
            ct);
        if (persistedUploadResult is not null)
        {
            return;
        }

        logger.LogWarning(
            "Beneficiary onboarding step completion stopped because Prospect could not persist the document upload result for prospect {ProspectId} and step {StepName}",
            prospectId,
            stepName);
        throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, "Prospect or onboarding step was not found.");
    }
}
