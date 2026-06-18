namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents a document downloaded from Prospect with the metadata required for downstream upload.
/// </summary>
/// <param name="Content">The document bytes.</param>
/// <param name="ContentType">The document MIME type.</param>
/// <param name="FileName">The document business file name.</param>
public sealed record ProspectDocumentContentResponse(
    byte[] Content,
    string ContentType,
    string FileName);
