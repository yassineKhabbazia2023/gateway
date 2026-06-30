using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;

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
    private const string OptionalDocumentType = "AUTRES";

    /// <inheritdoc />
    protected override string StepName => "SupportingDocuments";

    /// <inheritdoc />
    protected override string DisplayName => "supporting documents";

    /// <inheritdoc />
    public override async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct)
    {
        await ValidateSupportingDocumentsReadyAsync(prospectId, ct);
        return await base.CompleteAsync(prospectId, stepName, ct);
    }

    /// <inheritdoc />
    public override async Task<DocumentExternalUploadBatchResult> CompleteAsync(
        int prospectId,
        string stepName,
        CancellationToken ct,
        int? currentUserId)
    {
        await ValidateSupportingDocumentsReadyAsync(prospectId, ct);
        return await base.CompleteAsync(prospectId, stepName, ct, currentUserId);
    }

    private async Task ValidateSupportingDocumentsReadyAsync(
        int prospectId,
        CancellationToken ct)
    {
        var requirements = await prospectClient.GetDocumentRequirementsAsync(prospectId, ct);
        if (requirements is null)
        {
            logger.LogWarning(
                "Could not retrieve document requirements for prospect {ProspectId}. Skipping supporting documents validation.",
                prospectId);
            return;
        }

        var missingDocTypes = requirements.Documents
            .Where(IsRequiredAndMissing)
            .Select(req => req.Type)
            .ToList();

        if (missingDocTypes.Count > 0)
        {
            var missing = string.Join(", ", missingDocTypes);
            logger.LogWarning(
                "Supporting documents step completion blocked for prospect {ProspectId}: missing required documents: {MissingDocTypes}",
                prospectId,
                missing);

            throw new GatewayException(
                StatusCodes.Status422UnprocessableEntity,
                "SupportingDocumentsStepNotReady",
                $"Cannot complete Supporting Documents step: required documents missing ({missing}). Step must be InReview.");
        }
    }

    private static bool IsRequiredAndMissing(RequiredDocumentItem requirement)
    {
        var isRequired = requirement.MaxFiles > 0;
        var isMissing = requirement.Documents.Count == 0;
        var isOptionalType = string.Equals(requirement.Type, OptionalDocumentType, StringComparison.OrdinalIgnoreCase);

        return isRequired && isMissing && !isOptionalType;
    }
}
