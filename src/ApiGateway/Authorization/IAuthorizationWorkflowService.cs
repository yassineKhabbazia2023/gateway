using ApiGateway.Authorization.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Authorization
{
    public interface IAuthorizationWorkflowService
    {
        Task<OperationResult> TryUpdateAuthorizationsAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response);
    }
}
