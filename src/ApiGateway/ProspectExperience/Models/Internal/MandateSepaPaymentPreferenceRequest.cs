namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the downstream Mandat SEPA payment preference request.
/// </summary>
public sealed class MandateSepaPaymentPreferenceRequest
{
    /// <summary>
    /// Gets or sets the uploaded RIB document identifier.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the account holder.
    /// </summary>
    public string AccountHolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account holder address.
    /// </summary>
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
    public string Iban { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the BIC.
    /// </summary>
    public string Bic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signature recipient email.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signature recipient first name.
    /// </summary>
    public string RecipientFirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signature recipient last name.
    /// </summary>
    public string RecipientLastName { get; set; } = string.Empty;
}
