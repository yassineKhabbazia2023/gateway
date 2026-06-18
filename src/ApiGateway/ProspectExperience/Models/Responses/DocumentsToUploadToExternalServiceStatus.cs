using System.Text.Json.Serialization;

namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Describes why a document upload plan contains or does not contain documents.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentsToUploadToExternalServiceStatus
{
    /// <summary>Documents must be uploaded to the external service.</summary>
    PendingDocuments,

    /// <summary>The step is open but no document must be uploaded.</summary>
    NoDocumentsToUpload,

    /// <summary>The step is already completed and no external upload must be performed.</summary>
    StepAlreadyCompleted
}
