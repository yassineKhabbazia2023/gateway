
namespace ApiGateway.Models;

public class Account
{
    public int AccountId { get; set; }

    public Guid? AccountGlobalUniqueId { get; set; }

    public string? AccountNumber { get; set; }
}