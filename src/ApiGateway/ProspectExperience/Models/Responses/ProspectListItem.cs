using ApiGateway.ProspectExperience.Enum;

namespace ApiGateway.ProspectExperience.Models.Responses;

public class ProspectListItem
{
    public int AccountId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public AccountType AccountType { get; set; }
    public string LegalName { get; set; } = default!;
    public ProspectSignatoryListItem Signatory { get; set; } = default!;
    public ProspectStepCode StepCode { get; set; }
}
