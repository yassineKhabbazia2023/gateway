namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Outcome enumeration for supporting document upload.
/// </summary>
public enum UploadSupportingDocumentOutcome
{
    /// <summary>
    /// Upload succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// Validation error (invalid document type, missing file, etc.).
    /// </summary>
    ValidationError,

    /// <summary>
    /// File size exceeds limit.
    /// </summary>
    FileTooLarge,

    /// <summary>
    /// Prospect not found.
    /// </summary>
    ProspectNotFound
}
