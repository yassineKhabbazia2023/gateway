using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Offer.Model;

public class CreateCompanyRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int AccountId { get; set; }

    [Required]
    [MaxLength(100)]
    public IReadOnlyList<int> Contacts { get; set; } = [];

    public IReadOnlyList<ContactFunctions>? ContactFunctions { get; set; }

    /// <summary>
    /// Company registration number (SIREN).
    /// Maps to: <c>reg_no</c>.
    /// </summary>
    public required string RegistrationNumber { get; set; }

    /// <summary>
    /// Indicates whether the company is not yet registered.
    /// Maps to: <c>not_yet_registered</c>.
    /// Typically <c>false</c>.
    /// </summary>
    public required bool NotYetRegistered { get; set; }

    /// <summary>
    /// Type of accounting applied for the company.
    /// Maps to: <c>accounting_type</c>.
    /// </summary>
    public string? AccountingType { get; set; }

    /// <summary>
    /// Company country code.
    /// Maps to: <c>country_alpha2</c>.
    /// </summary>
    public required string CountryCode { get; set; }
}
