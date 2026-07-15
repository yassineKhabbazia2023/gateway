namespace ApiGateway.Authorization.Consts;

public static class PermissionCodes
{
    public const string PennylaneAccess = "CLPEN001";

    /// <summary>Global collaborator right for prospect onboarding (commercial proposal and engagement letter).</summary>
    public const string ProspectOnboardingCollaboratorAccess = "COPROS001";

    /// <summary>Per-entity client right to access the commercial proposal.</summary>
    public const string CommercialProposalClientAccess = "CLPCONF004";

    /// <summary>Per-entity client right to access the engagement letter.</summary>
    public const string EngagementLetterClientAccess = "CLPCONF006";
}
