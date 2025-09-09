using System.Text.Json.Serialization;

namespace ApiGateway.Models;

public class Contact
{
    [JsonIgnore]
    public int ContactId { get; set; }

    public Guid? ContactGlobalUniqueId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string Email { get; set; }

    public string? LandPhone { get; set; }

    public string? MobilePhone { get; set; }

    public string? Type { get; set; }

    public string? Status { get; set; }

    public string? PersonaName { get; set; }

    public string? Office { get; set; }

    public DateTime? CreationDate { get; set; }

    public bool IsActive { get; set; }

    public bool? IsCustomerRelation { get; set; }

    public int ActionLevel { get; set; }

    public IEnumerable<Label>? Labels { get; set; }
}
