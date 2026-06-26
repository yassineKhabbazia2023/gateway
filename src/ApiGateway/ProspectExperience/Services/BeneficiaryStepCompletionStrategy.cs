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
}
