namespace ApiGateway.ProspectExperience.Models.Internal;

public sealed class CreateRolesBulkRequest
{
    public required IReadOnlyCollection<CreateRolesBulkItem> Contacts { get; set; }
}

public sealed class CreateRolesBulkItem
{
    public required int ContactId { get; set; }

    public bool? IsSignatory { get; set; }

    public bool? IsFavorite { get; set; }

    public bool? IsDelegation { get; set; }

    public bool? IncludePennylaneAccess { get; set; }

    public bool? ContactFlagPortailFactures { get; set; }

    public string? RoleCode { get; set; }
}
