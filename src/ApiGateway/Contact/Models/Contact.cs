using System.Text.Json.Serialization;

namespace ApiGateway.Contact.Models;

public class PagingResult
{
    [JsonPropertyName("items")]
    public List<Contact>? Items { get; set; }
}
public class Contact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("landPhone")]
    public string? LandPhone { get; set; }

    [JsonPropertyName("mobilePhone")]
    public string? MobilePhone { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
