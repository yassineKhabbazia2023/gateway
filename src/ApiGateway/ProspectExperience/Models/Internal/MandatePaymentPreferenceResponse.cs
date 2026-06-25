namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the internal Mandat payment preference response consumed by Gateway.
/// </summary>
public sealed class MandatePaymentPreferenceResponse
{
    /// <summary>
    /// Gets or sets the selected payment type.
    /// </summary>
    public string? PaymentType { get; set; }

    /// <summary>
    /// Gets or sets the account identifier when a signed mandate must be uploaded.
    /// </summary>
    public int? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the Prospect RIB document identifier to upload with the signed mandate.
    /// </summary>
    public int? RibDocumentId { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded signed mandate PDF.
    /// </summary>
    public string? SignedMandatePdfBase64 { get; set; }

    /// <summary>
    /// Gets or sets the signed mandate content type.
    /// </summary>
    public string? SignedMandateContentType { get; set; }

    /// <summary>
    /// Gets or sets the signed mandate file name.
    /// </summary>
    public string? SignedMandateFileName { get; set; }

    /// <summary>
    /// Gets or sets the Prospect document identifier when the signed mandate was already persisted.
    /// </summary>
    public string? SignedMandateDocumentId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the response contains a signed mandate PDF for upload.
    /// </summary>
    public bool HasSignedMandate => !string.IsNullOrWhiteSpace(SignedMandatePdfBase64)
        && AccountId.HasValue
        && RibDocumentId.HasValue;
}
