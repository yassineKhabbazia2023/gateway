namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Result of uploading a supporting document to Prospect service.
/// </summary>
public class UploadSupportingDocumentResult
{
    /// <summary>
    /// Gets or sets the outcome of the upload operation.
    /// </summary>
    public UploadSupportingDocumentOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the uploaded document identifier (when outcome is Success).
    /// </summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the error code (when outcome is ValidationError or FileTooLarge).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message (when outcome is ValidationError or FileTooLarge).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the validation field name (when outcome is ValidationError).
    /// </summary>
    public string? FieldName { get; set; }

    public static UploadSupportingDocumentResult Success(int documentId) => new()
    {
        Outcome = UploadSupportingDocumentOutcome.Success,
        DocumentId = documentId
    };

    public static UploadSupportingDocumentResult ValidationError(string fieldName, string errorCode, string errorMessage) => new()
    {
        Outcome = UploadSupportingDocumentOutcome.ValidationError,
        FieldName = fieldName,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static UploadSupportingDocumentResult FileTooLarge(string errorCode, string errorMessage) => new()
    {
        Outcome = UploadSupportingDocumentOutcome.FileTooLarge,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static UploadSupportingDocumentResult ProspectNotFound() => new()
    {
        Outcome = UploadSupportingDocumentOutcome.ProspectNotFound
    };
}
