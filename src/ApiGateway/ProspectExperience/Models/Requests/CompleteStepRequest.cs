namespace ApiGateway.ProspectExperience.Models.Requests;

/// <summary>
/// Request used to complete a prospect onboarding step.
/// </summary>
public sealed class CompleteStepRequest
{
    /// <summary>
    /// Gets or sets the public onboarding step name.
    /// </summary>
    public required string StepName { get; set; }
}
