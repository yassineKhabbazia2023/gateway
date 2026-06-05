namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents the Account downstream result for the prospect-only contact check.
/// </summary>
public enum ProspectOnlyContactResult
{
    /// <summary>
    /// The contact was not found by the Account downstream service.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// The contact exists but is not prospect-only.
    /// </summary>
    NotProspectOnly = 1,

    /// <summary>
    /// The contact exists and has only prospect roles.
    /// </summary>
    ProspectOnly = 2
}
