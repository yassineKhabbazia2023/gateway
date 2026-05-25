using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Models.Internal;

public class CreateContactRequest
{
    public string Email { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MobilePhone { get; set; }
    public string? LandPhone { get; set; }
    public string? OfficeCode { get; set; }
    public string? JobDescription { get; set; }
    public Guid OldId { get; set; }

    public static CreateContactRequest FromSignatory(SignatoryDto signatory, string? officeCode) => new()
    {
        Email = signatory.Email,
        FirstName = signatory.FirstName,
        LastName = signatory.LastName,
        MobilePhone = signatory.MobilePhone,
        OfficeCode = officeCode,
        JobDescription = signatory.JobTitle,
        OldId = Guid.NewGuid()
    };
}
