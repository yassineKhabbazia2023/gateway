using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Request model for uploading supporting documents.
/// </summary>
public class UploadSupportingDocumentRequest
{
    /// <summary>
    /// Gets or sets the document type (KBIS, STATUTS, etc.).
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file to upload.
    /// </summary>
    public IFormFile File { get; set; } = null!;
}
