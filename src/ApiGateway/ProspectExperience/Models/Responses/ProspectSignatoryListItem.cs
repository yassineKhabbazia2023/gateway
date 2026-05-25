namespace ApiGateway.ProspectExperience.Models.Responses;

public class ProspectSignatoryListItem
{
    public int ContactId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
}
