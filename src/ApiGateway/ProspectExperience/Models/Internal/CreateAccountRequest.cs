using System.ComponentModel.DataAnnotations;
using ApiGateway.ProspectExperience.Enum;

namespace ApiGateway.ProspectExperience.Models.Internal;

public class CreateAccountRequest
{
    public string AccountNumber { get; set; } = default!;
    public string LegalName { get; set; } = default!;
    public string Siret { get; set; } = default!;

    [Required]
    public required AccountType AccountType { get; set; }
}
