using ApiGateway.Authorization.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Pennylane
{
    public interface IPennylaneAuthorizationService
    {
        Task<bool> HasPennylaneAccessAsync(AuthorizationUpdateRequest request);

        Task<OperationResult> TryUpdatePennylaneRoleAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response);

        Task<OperationResult> TryHandlePennylaneProvisioningAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response);

        Task<OperationResult> TryRevokePennylaneAccessAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response);
    }
}
