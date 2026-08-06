namespace ApiGateway.ProspectExperience.Models.Internal;

public class AddressRequest
{
    public string? Street { get; set; }

    public string Department { get; set; } = default!;

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public string Region { get; set; } = default!;

    public string Country { get; set; } = default!;
}
