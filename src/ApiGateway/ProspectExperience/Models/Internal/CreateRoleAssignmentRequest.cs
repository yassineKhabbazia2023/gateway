namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents a role assignment payload sent to the prospect role endpoint.
/// </summary>
public sealed class CreateRoleAssignmentRequest
{
    /// <summary>
    /// Gets or sets the contact identifier.
    /// </summary>
    public required int ContactId { get; set; }

    /// <summary>
    /// Gets or sets the account identifier.
    /// </summary>
    public required int AccountId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact must be marked as signatory.
    /// </summary>
    public required bool IsSignatory { get; set; }
}
