using System.Text.Json.Serialization;

namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the semantic completed milestones reported by Gateway to Prospect during the resumable creation orchestration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProspectCreationMilestone
{
    /// <summary>
    /// The Akuiteo customer creation step completed successfully.
    /// </summary>
    AkuiteoCustomerCreated,

    /// <summary>
    /// The Akuiteo contact creation step completed successfully.
    /// </summary>
    AkuiteoContactCreated,

    /// <summary>
    /// The Rydge account creation step completed successfully.
    /// </summary>
    RydgeAccountCreated,

    /// <summary>
    /// The Rydge contact creation step completed successfully.
    /// </summary>
    RydgeContactCreated,

    /// <summary>
    /// The account role assignment and synchronization confirmation completed successfully.
    /// </summary>
    RolesAssigned
}
