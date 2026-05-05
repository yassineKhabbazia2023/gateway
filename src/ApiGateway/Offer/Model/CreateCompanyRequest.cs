using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    public string? HubName { get; set; }

    /// <summary>
    /// Company registration number (SIREN).
    /// Maps to: <c>reg_no</c>.
    /// When null, this field will be omitted from the JSON payload.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistrationNumber { get; set; }

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

    /// <summary>
    /// Requested plan code from the Offer system.
    /// Used by Pennylane to compare with the actual SaaS plan.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestedPlanCode { get; set; }

    /// <summary>
    /// The number of user you want to define in the current selected plan.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumberOfUsers { get; set; }
}
