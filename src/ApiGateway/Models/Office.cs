namespace ApiGateway.Models;

public class Office
{
    public int OfficeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public int AddressId { get; set; }

    public Address? Address { get; set; }
}
