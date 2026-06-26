namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Response model for supporting document upload.
/// </summary>
public class UploadSupportingDocumentResponse
{
    /// <summary>
    /// Gets or sets the uploaded document identifier.
    /// </summary>
    public required int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supporting documents step was completed after this upload.
    /// </summary>
    public bool StepCompleted { get; set; }
}
