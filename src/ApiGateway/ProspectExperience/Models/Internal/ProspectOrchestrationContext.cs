namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Carries shared orchestration state used for logging and error reporting during prospect creation.
/// </summary>
public sealed class ProspectOrchestrationContext
{
    /// <summary>
    /// Gets or sets the SIRET being orchestrated.
    /// </summary>
    public required string Siret { get; set; }

    /// <summary>
    /// Gets or sets the created prospect identifier when available.
    /// </summary>
    public int? ProspectId { get; set; }

    /// <summary>
    /// Gets or sets the created account number when available.
    /// </summary>
    public string? AccountNumber { get; set; }
}
