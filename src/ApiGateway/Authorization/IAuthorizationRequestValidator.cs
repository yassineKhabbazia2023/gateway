using ApiGateway.Authorization.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Authorization
{
    public interface IAuthorizationRequestValidator
    {
        bool TryValidateRequest(AuthorizationUpdateRequest request, out ActionResult<AuthorizationUpdateResponse> errorResult);

        bool TryValidatePennylaneAuthorizationDetails(AuthorizationUpdateRequest request, out ActionResult<AuthorizationUpdateResponse> errorResult);
    }
}
