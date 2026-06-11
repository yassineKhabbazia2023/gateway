using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Exceptions;

public class ProspectOrchestrationException : GatewayException
{
    private static readonly IReadOnlyDictionary<int, string> StepNames = new Dictionary<int, string>
    {
        [1] = "CheckSiretNotInAkuiteo",
        [2] = "FetchInpi",
        [3] = "CreateProspect",
        [4] = "CreateAkuiteoCustomer",
        [5] = "CreateAkuiteoContact",
        [6] = "CreateRydgeAccount",
        [7] = "CreateRydgeContact",
        [8] = "PatchProspectIds",
        [9] = "AssignAccountRoles",
        [10] = "PrepareCreationResume",
        [11] = "ConfirmRoleSynchronization",
        [12] = "PersistBeneficiaries"
    };

    public int Step { get; }
    public string StepName { get; }
    public string? Siret { get; }
    public int? ProspectId { get; }
    public string? AccountNumber { get; }

    public ProspectOrchestrationException(
        int step,
        string? siret = null,
        int? prospectId = null,
        string? accountNumber = null,
        Exception? inner = null)
        : base(StatusCodes.Status422UnprocessableEntity,
               Errors.ProspectOrchestrationFailedCode,
               string.Format(Errors.ProspectOrchestrationFailedMessage, step))
    {
        Step = step;
        StepName = StepNames.TryGetValue(step, out var name) ? name : $"Step{step}";
        Siret = siret;
        ProspectId = prospectId;
        AccountNumber = accountNumber;
        InnerExceptionOverride = inner;
    }

    public Exception? InnerExceptionOverride { get; }

    public override void EnrichTelemetry(IDictionary<string, string> properties)
    {
        properties["Step"] = Step.ToString();
        properties["StepName"] = StepName;
        if (Siret is not null) properties["Siret"] = Siret;
        if (ProspectId is not null) properties["ProspectId"] = ProspectId.Value.ToString();
        if (AccountNumber is not null) properties["AccountNumber"] = AccountNumber;
    }
}
