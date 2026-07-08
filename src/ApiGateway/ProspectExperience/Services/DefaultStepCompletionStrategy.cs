using System.Net;
using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Completes onboarding steps that do not require external document orchestration.
/// </summary>
public sealed class DefaultStepCompletionStrategy(
    IProspectApiClient prospectClient,
    ILogger<DefaultStepCompletionStrategy> logger) : IProspectStepCompletionStrategy
{
    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public bool CanHandle(string stepName)
    {
        return !string.IsNullOrWhiteSpace(stepName);
    }

    /// <inheritdoc />
    public async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int accountId,
        string stepName,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Completing onboarding step {StepName} for account {AccountId} without external document upload",
            stepName,
            accountId);

        try
        {
            await prospectClient.CompleteStepAsync(accountId, stepName, ct);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GatewayException(
                StatusCodes.Status404NotFound,
                Errors.NullArgumentCode,
                "Onboarding step was not found.");
        }

        return new DocumentExternalUploadBatchResult([], []);
    }

    /// <inheritdoc />
    public async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int accountId,
        string stepName,
        CancellationToken ct,
        int? currentUserId)
    {
        logger.LogInformation(
            "Completing onboarding step {StepName} for account {AccountId} without external document upload",
            stepName,
            accountId);

        try
        {
            await prospectClient.CompleteStepAsync(accountId, stepName, ct, currentUserId);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GatewayException(
                StatusCodes.Status404NotFound,
                Errors.NullArgumentCode,
                "Onboarding step was not found.");
        }

        return new DocumentExternalUploadBatchResult([], []);
    }
}
