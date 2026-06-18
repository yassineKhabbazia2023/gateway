namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Contains the external account number and document identifiers to upload for a prospect onboarding step.
/// </summary>
/// <param name="AkuiteoAccountNumber">The Akuitéo account number used for downstream document upload.</param>
/// <param name="DocumentIds">The document identifiers to upload.</param>
/// <param name="Status">The upload-plan status.</param>
public sealed record DocumentsToUploadToExternalServiceResponse(
    string? AkuiteoAccountNumber,
    IReadOnlyList<int> DocumentIds,
    DocumentsToUploadToExternalServiceStatus Status);
