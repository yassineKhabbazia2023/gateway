namespace ApiGateway.Contact.Models;

public class PagingResult
{
    public List<Contact>? Items { get; set; }
}
public class Contact
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
}
