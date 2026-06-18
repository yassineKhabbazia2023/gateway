namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Contains the successful and failed document upload identifiers for one onboarding step orchestration.
/// </summary>
/// <param name="SucceededDocumentIds">The document identifiers successfully uploaded to the external service.</param>
/// <param name="FailedDocumentIds">The document identifiers that failed to upload to the external service.</param>
public sealed record DocumentExternalUploadBatchResult(
    IReadOnlyList<int> SucceededDocumentIds,
    IReadOnlyList<int> FailedDocumentIds);
