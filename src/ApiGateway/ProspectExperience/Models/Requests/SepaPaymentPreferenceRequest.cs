using System.ComponentModel.DataAnnotations;

namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Represents the multipart Gateway request to generate a SEPA mandate.
/// </summary>
public sealed class SepaPaymentPreferenceRequest
{
    /// <summary>
    /// Gets or sets the uploaded RIB file.
    /// </summary>
    [Required]
    public IFormFile? File { get; set; }

    /// <summary>
    /// Gets or sets the account holder.
    /// </summary>
    [Required]
    public string AccountHolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account holder address.
    /// </summary>
    [Required]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the complementary account holder address line.
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the account holder city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the account holder country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the account holder postal code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the IBAN.
    /// </summary>
    [Required]
    public string Iban { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the BIC.
    /// </summary>
    [Required]
    public string Bic { get; set; } = string.Empty;
}
