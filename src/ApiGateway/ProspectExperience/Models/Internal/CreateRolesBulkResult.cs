namespace ApiGateway.ProspectExperience.Models.Internal;

public sealed class CreateRolesBulkResult
{
    public List<CreateRolesBulkItemResult> Succeeded { get; set; } = new();

    public List<CreateRolesBulkItemResult> Failed { get; set; } = new();
}

public sealed class CreateRolesBulkItemResult
{
    public int ContactId { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}
