namespace ApiGateway.ProspectExperience.Models.Responses;

/// <summary>
/// Represents the current payment preference response.
/// </summary>
public sealed class PaymentPreferenceResponse
{
    /// <summary>
    /// Gets or sets the selected payment type.
    /// </summary>
    public string? PaymentType { get; set; }
}
