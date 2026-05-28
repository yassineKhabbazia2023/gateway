namespace ApiGateway.ProspectExperience.Models.Internal;

public class InpiCompanyInfo
{
    public string LegalName { get; set; } = default!;
    public string Siret { get; set; } = default!;
    public string Siren { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string LegalForm { get; set; } = default!;
    public string NafCode { get; set; } = default!;
    public decimal ShareCapital { get; set; }
    public string RegionCode { get; set; } = string.Empty;

}
