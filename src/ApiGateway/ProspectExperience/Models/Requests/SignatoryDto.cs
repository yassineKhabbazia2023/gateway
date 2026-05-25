namespace ApiGateway.ProspectExperience.Models.Requests;

public class SignatoryDto
{
    public string Title { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public string FirstName { get; set; } = default!;

    public string JobTitle { get; set; } = default!;

    public string Department { get; set; } = default!;

    public string CompanyRole { get; set; } = default!;

    public string[] ContactTypes { get; set; } = [];

    public string Email { get; set; } = default!;

    public string MobilePhone { get; set; } = default!;
}
