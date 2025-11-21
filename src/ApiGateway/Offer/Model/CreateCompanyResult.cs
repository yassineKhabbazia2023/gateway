namespace ApiGateway.Offer.Model;

public class CreateCompanyResult
{
    public required PennylaneCompany Company { get; set; }

    public required string Status { get; set; }

    public List<UserAdditionResult>? UserResults { get; set; }
}

public class UserAdditionResult
{
    public int ContactId { get; set; }

    public required string Email { get; set; }

    public bool Success { get; set; }

    public string? Error { get; set; }
}

public class PennylaneCompany
{
    public required string Id { get; set; }

    public required string FirmId { get; set; }

    public string? RegNo { get; set; }

    public required string Name { get; set; }

    public string? TradeName { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? CountryAlpha2 { get; set; }

    public string? SaasPlan { get; set; }

    public string? FiscalCategory { get; set; }

    public string? FiscalRegime { get; set; }

    public string? LegalFormCode { get; set; }

    public string? VatFrequency { get; set; }

    public int? VatDayOfMonth { get; set; }

    public DateTime? TosAcceptedAt { get; set; }

    public string[]? SupplierInvoiceTransmissionEmails { get; set; }

    public string[]? CustomerInvoiceTransmissionEmails { get; set; }
}
