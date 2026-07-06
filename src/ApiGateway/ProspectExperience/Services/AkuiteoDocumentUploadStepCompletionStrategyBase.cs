using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Base class for onboarding step completion strategies that upload documents to Akuitéo.
/// </summary>
public abstract class AkuiteoDocumentUploadStepCompletionStrategyBase(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    ILogger logger) : IProspectStepCompletionStrategy
{
    /// <summary>
    /// Gets the step name handled by this strategy.
    /// </summary>
    protected abstract string StepName { get; }

    /// <summary>
    /// Gets the display name for logging (e.g., "beneficiary", "supporting documents").
    /// </summary>
    protected abstract string DisplayName { get; }

    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool CanHandle(string stepName)
    {
        return string.Equals(StepName, stepName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public virtual async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int accountId,
        string stepName,
        CancellationToken ct)
    {
        return await CompleteAsync(
            accountId,
            stepName,
            completeStepAsync: () => prospectClient.CompleteStepAsync(accountId, stepName, ct),
            ct);
    }

    /// <inheritdoc />
    public virtual async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int accountId,
        string stepName,
        CancellationToken ct,
        int? currentUserId)
    {
        return await CompleteAsync(
            accountId,
            stepName,
            completeStepAsync: () => prospectClient.CompleteStepAsync(accountId, stepName, ct, currentUserId),
            ct);
    }

    /// <summary>
    /// Validates the upload plan before proceeding with document upload.
    /// Override to add custom validation logic based on uploadPlan status.
    /// </summary>
    /// <param name="uploadPlan">The upload plan returned by Prospect service.</param>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="ct">The cancellation token.</param>
    protected virtual Task ValidateUploadPlanAsync(
        DocumentsToUploadToExternalServiceResponse uploadPlan,
        int prospectId,
        string stepName,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int accountId,
        string stepName,
        Func<Task> completeStepAsync,
        CancellationToken ct)
    {
        var prospectId = await ResolveProspectIdAsync(accountId, ct);
        var uploadPlan = await prospectClient.GetDocumentsToUploadToExternalServiceAsync(
            prospectId,
            stepName,
            ct);
        if (uploadPlan is null)
        {
            logger.LogWarning(
                "{DisplayName} onboarding step completion stopped because Prospect could not return a document upload plan for prospect {ProspectId} and step {StepName}",
                DisplayName,
                prospectId,
                stepName);
            throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, "Prospect or onboarding step was not found.");
        }

        await ValidateUploadPlanAsync(uploadPlan, prospectId, stepName, ct);

        if (uploadPlan.DocumentIds.Count == 0)
        {
            logger.LogInformation(
                "{DisplayName} onboarding step completion stopped without external upload for prospect {ProspectId} and step {StepName}. Reason: {UploadPlanStatus}",
                DisplayName,
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
    /// Resolves the prospect identifier needed by existing prospect-scoped document helper endpoints.
    /// </summary>
    /// <param name="accountId">The account identifier received by the migrated complete-step endpoint.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The linked prospect identifier when found; otherwise the provided account identifier.</returns>
    private async Task<int> ResolveProspectIdAsync(int accountId, CancellationToken ct)
    {
        return await prospectClient.GetProspectIdByAccountIdAsync(accountId, ct) ?? accountId;
    }

    /// <summary>
    /// Uploads the pending documents sequentially and records each document outcome.
    /// </summary>
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

                succeededDocumentIds.Add(documentId);
                continue;
            }

            logger.LogWarning(
                "{DisplayName} document upload to Registry for Akuitéo failed. ProspectId: {ProspectId}, StepName: {StepName}, DocumentId: {DocumentId}, AccountNumber: {AccountNumber}, FileName: {FileName}",
                DisplayName,
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
            "{DisplayName} onboarding step completion stopped because Prospect could not persist the document upload result for prospect {ProspectId} and step {StepName}",
            DisplayName,
            prospectId,
            stepName);
        throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, "Prospect or onboarding step was not found.");
    }
}
