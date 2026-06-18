namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Returns the consolidated document upload result for a prospect onboarding step.
/// </summary>
/// <param name="SucceededDocumentIds">The document identifiers successfully uploaded to the external service.</param>
/// <param name="FailedDocumentIds">The document identifiers that failed to upload to the external service.</param>
public sealed record DocumentUploadResultResponse(
    IReadOnlyList<int> SucceededDocumentIds,
    IReadOnlyList<int> FailedDocumentIds);
