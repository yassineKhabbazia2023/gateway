namespace ApiGateway.ProspectExperience.Constants;

/// <summary>
/// Defines the local Gateway diagnostic step identifiers used for logs, exceptions, telemetry, and troubleshooting.
/// These values are not used to populate Prospect progress or failure payloads.
/// </summary>
public static class ProspectOrchestrationDiagnosticSteps
{
    /// <summary>
    /// Gets the diagnostic step identifier for the Akuiteo duplicate SIRET check.
    /// </summary>
    public const int CheckAkuiteoSiret = 1;

    /// <summary>
    /// Gets the diagnostic step identifier for the INPI company information retrieval.
    /// </summary>
    public const int FetchInpi = 2;

    /// <summary>
    /// Gets the diagnostic step identifier for the Prospect aggregate creation.
    /// </summary>
    public const int CreateProspect = 3;

    /// <summary>
    /// Gets the diagnostic step identifier for the Akuiteo customer creation.
    /// </summary>
    public const int CreateAkuiteoCustomer = 4;

    /// <summary>
    /// Gets the diagnostic step identifier for the Akuiteo contact creation.
    /// </summary>
    public const int CreateAkuiteoContact = 5;

    /// <summary>
    /// Gets the diagnostic step identifier for the Rydge account creation.
    /// </summary>
    public const int CreateRydgeAccount = 6;

    /// <summary>
    /// Gets the diagnostic step identifier for the Rydge contact creation.
    /// </summary>
    public const int CreateRydgeContact = 7;

    /// <summary>
    /// Gets the diagnostic step identifier for the Prospect finalization.
    /// </summary>
    public const int FinalizeProspect = 8;

    /// <summary>
    /// Gets the diagnostic step identifier for the account role assignment.
    /// </summary>
    public const int AssignRoles = 9;

    /// <summary>
    /// Gets the diagnostic-only step identifier for the resume preparation call executed before a pre-finalization retry.
    /// </summary>
    public const int PrepareCreationResume = 10;

    /// <summary>
    /// Gets the diagnostic-only step identifier for the post-role-assignment synchronization confirmation call.
    /// </summary>
    public const int ConfirmRoleSynchronization = 11;

    /// <summary>
    /// Gets the diagnostic step identifier for active INPI beneficiary persistence.
    /// </summary>
    public const int PersistBeneficiaries = 12;
}
