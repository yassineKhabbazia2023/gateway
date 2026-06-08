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

    /// <summary>
    /// Gets or sets the account number used to prepare the Prospect customer in Gigya.
    /// </summary>
    public string AccountNumber { get; set; } = default!;

    /// <summary>
    /// Creates the Contact request used by Prospect orchestration.
    /// </summary>
    /// <param name="signatory">The Prospect signatory.</param>
    /// <param name="officeCode">The optional office code.</param>
    /// <param name="accountNumber">The Prospect account number.</param>
    /// <returns>The Contact creation request.</returns>
    public static CreateContactRequest FromSignatory(SignatoryDto signatory, string? officeCode, string accountNumber) => new()
    {
        Email = signatory.Email,
        FirstName = signatory.FirstName,
        LastName = signatory.LastName,
        MobilePhone = signatory.MobilePhone,
        OfficeCode = officeCode,
        JobDescription = signatory.JobTitle,
        OldId = Guid.NewGuid(),
        AccountNumber = accountNumber
    };
}
