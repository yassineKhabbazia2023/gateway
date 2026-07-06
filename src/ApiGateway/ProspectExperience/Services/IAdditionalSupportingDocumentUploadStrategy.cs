using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.ProspectExperience.Services;

/// <summary>
/// Uploads complementary supporting documents to Akuitéo without completing an onboarding step.
/// </summary>
public interface IAdditionalSupportingDocumentUploadStrategy
{
    /// <summary>
    /// Uploads a stored complementary supporting document to Akuitéo and persists the upload result.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="documentId">The stored document identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The external document upload result.</returns>
    Task<DocumentExternalUploadBatchResult> UploadAsync(int prospectId, int documentId, CancellationToken ct);
}
