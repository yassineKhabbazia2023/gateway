using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Offer.Model;

public class CreateCompanyRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int AccountId { get; set; }

    [Required]
    [MaxLength(100)]
    public IReadOnlyList<int> Contacts { get; set; } = [];
}
