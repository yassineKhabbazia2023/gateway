namespace ApiGateway.ProspectExperience.Models.Requests;

public class CreateProspectRequest
{
    public string Siret { get; set; } = default!;

    public string LegalForm { get; set; } = default!;

    public string LegalStructure { get; set; } = default!;

    public int? CaseManagerContactId { get; set; }

    public int AccountManagerContactId { get; set; }

    public string? OfficeCode { get; set; }

    public string Department { get; set; } = default!;

    public string Region { get; set; } = default!;

    public string Country { get; set; } = default!;

    public bool? DossierCAC { get; set; }

    public SignatoryDto Signatory { get; set; } = default!;
}
