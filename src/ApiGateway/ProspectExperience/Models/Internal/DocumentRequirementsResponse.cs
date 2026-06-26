namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Response containing document requirements for a prospect.
/// </summary>
public class DocumentRequirementsResponse
{
    /// <summary>
    /// Gets or sets the list of required documents with upload status.
    /// </summary>
    public List<RequiredDocumentItem> Documents { get; set; } = [];
}

/// <summary>
/// Required document item with upload count.
/// </summary>
public class RequiredDocumentItem
{
    /// <summary>
    /// Gets or sets the document type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum files allowed.
    /// </summary>
    public int MaxFiles { get; set; }

    /// <summary>
    /// Gets or sets the list of uploaded documents (simplified - only count matters).
    /// </summary>
    public List<object> Documents { get; set; } = [];
}
