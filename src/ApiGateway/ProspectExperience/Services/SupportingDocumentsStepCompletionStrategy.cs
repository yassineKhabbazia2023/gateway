namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Completes the supporting documents onboarding step by uploading pending documents to Akuitéo.
/// </summary>
public sealed class SupportingDocumentsStepCompletionStrategy(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    ILogger<SupportingDocumentsStepCompletionStrategy> logger)
    : AkuiteoDocumentUploadStepCompletionStrategyBase(registryClient, prospectClient, logger)
{
    /// <inheritdoc />
    protected override string StepName => "SupportingDocuments";

    /// <inheritdoc />
    protected override string DisplayName => "supporting documents";
}
