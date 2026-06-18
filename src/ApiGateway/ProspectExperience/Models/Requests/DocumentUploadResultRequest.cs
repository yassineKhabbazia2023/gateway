namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Carries the document upload result for a prospect onboarding step.
/// </summary>
/// <param name="StepName">The public onboarding step name.</param>
/// <param name="SucceededDocumentIds">The document identifiers successfully uploaded to the external service.</param>
/// <param name="FailedDocumentIds">The document identifiers that failed to upload to the external service.</param>
public sealed record DocumentUploadResultRequest(
    string StepName,
    IReadOnlyList<int> SucceededDocumentIds,
    IReadOnlyList<int> FailedDocumentIds);
