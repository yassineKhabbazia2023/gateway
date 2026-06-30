using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Completes the beneficiary onboarding step by uploading pending documents to Akuitéo.
/// </summary>
public sealed class BeneficiaryStepCompletionStrategy(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    ILogger<BeneficiaryStepCompletionStrategy> logger)
    : AkuiteoDocumentUploadStepCompletionStrategyBase(registryClient, prospectClient, logger)
{
    /// <inheritdoc />
    protected override string StepName => "Beneficiary";

    /// <inheritdoc />
    protected override string DisplayName => "beneficiary";

    /// <inheritdoc />
    protected override Task ValidateUploadPlanAsync(
        DocumentsToUploadToExternalServiceResponse uploadPlan,
        int prospectId,
        string stepName,
        CancellationToken ct)
    {
        if (uploadPlan.Status == DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload)
        {
            logger.LogWarning(
                "Beneficiary step completion blocked for prospect {ProspectId}: required identity documents not uploaded for all beneficiaries",
                prospectId);

            throw new GatewayException(
                StatusCodes.Status422UnprocessableEntity,
                "BeneficiaryStepNotReady",
                "Cannot complete Beneficiary step: required identity documents not uploaded for all beneficiaries. Please ensure all beneficiaries have complete identity document sets before completing this step.");
        }

        return Task.CompletedTask;
    }
}
