using ApiGateway.Attributes;
using ApiGateway.Authorization.Consts;
using ApiGateway.Authorization.Models;
using ApiGateway.Pennylane;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Authorization
{
    [Route("gtw/authorization/api")]
    [Authorize]
    public class AuthorizationController : ControllerBase
    {
        private readonly IAuthorizationRequestValidator _requestValidator;
        private readonly IPennylaneAuthorizationService _pennylaneAuthorizationService;
        private readonly IAuthorizationWorkflowService _authorizationWorkflowService;
        private readonly ILogger<AuthorizationController> _logger;

        public AuthorizationController(
            IAuthorizationRequestValidator requestValidator,
            IPennylaneAuthorizationService pennylaneAuthorizationService,
            IAuthorizationWorkflowService authorizationWorkflowService,
            ILogger<AuthorizationController> logger)
        {
            _requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
            _pennylaneAuthorizationService = pennylaneAuthorizationService ?? throw new ArgumentNullException(nameof(pennylaneAuthorizationService));
            _authorizationWorkflowService = authorizationWorkflowService ?? throw new ArgumentNullException(nameof(authorizationWorkflowService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("authorizations/update")]
        [RequirePermission("COUSER001", "COADMI004", "CLUSER004")]
        [ProducesResponseType(typeof(AuthorizationUpdateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthorizationUpdateResponse>> CreateOrUpdateAuthorizationsAsync([FromBody] AuthorizationUpdateRequest request)
        {
            if (!_requestValidator.TryValidateRequest(request, out var validationError))
            {
                return validationError;
            }

            var response = InitializeResponse(request);

            _logger.LogInformation("Starting authorization workflow for ContactId: {ContactId}, AccountId: {AccountId}",
                request.Authorization.ContactId,
                request.Authorization.AccountId);

            var hasPennylanePermission = request.PermissionsCodes.Contains(PermissionCodes.PennylaneAccess);

            if (hasPennylanePermission)
            {
                if (!_requestValidator.TryValidatePennylaneAuthorizationDetails(request, out var targetValidationError))
                {
                    return targetValidationError;
                }

                var alreadyHasAccess = await _pennylaneAuthorizationService.HasPennylaneAccessAsync(request);
                if (alreadyHasAccess)
                {
                    var roleUpdateResult = await _pennylaneAuthorizationService.TryUpdatePennylaneRoleAsync(request, response);
                    if (!roleUpdateResult.Success)
                    {
                        return roleUpdateResult.Error!;
                    }
                    return Ok(response);
                }

                var provisioningResult = await _pennylaneAuthorizationService.TryHandlePennylaneProvisioningAsync(request, response);
                if (!provisioningResult.Success)
                {
                    return provisioningResult.Error!;
                }
            }

            var authorizationUpdateResult = await _authorizationWorkflowService.TryUpdateAuthorizationsAsync(request, response);
            if (!authorizationUpdateResult.Success)
            {
                return authorizationUpdateResult.Error!;
            }

            return Ok(response);
        }

        private static AuthorizationUpdateResponse InitializeResponse(AuthorizationUpdateRequest request)
        {
            return new AuthorizationUpdateResponse
            {
                ContactId = request.Authorization!.ContactId,
                AccountId = request.Authorization.AccountId
            };
        }
    }
}
