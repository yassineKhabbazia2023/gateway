using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Authorization.Models
{
    public readonly record struct OperationResult(bool Success, ActionResult<AuthorizationUpdateResponse>? Error)
    {
        public static OperationResult Ok() => new(true, null);

        public static OperationResult Fail(ActionResult<AuthorizationUpdateResponse> error) => new(false, error);
    }
}
