namespace ApiGateway.Models;

/// <summary>
/// Réponse de GET api/serenity-eligibility (micro-service Account).
/// </summary>
public class SerenityEligibility
{
    public bool HasMadeChoice { get; set; }

    public int[] CandidateAccountIds { get; set; } = [];
}
