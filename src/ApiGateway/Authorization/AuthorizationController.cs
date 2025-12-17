using ApiGateway.Attributes;
using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Models;
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
        private readonly IAuthorizationService _authorizationService;
        private readonly IPennylaneService _pennylaneService;
        private readonly ILogger<AuthorizationController> _logger;

        public AuthorizationController(
            IAuthorizationService authorizationService,
            IPennylaneService pennylaneService,
            ILogger<AuthorizationController> logger)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _pennylaneService = pennylaneService ?? throw new ArgumentNullException(nameof(pennylaneService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("authorizations/create")]
        [RequirePermission("COUSER001", "COADMI004", "CLUSER004")]
        [ProducesResponseType(typeof(AuthorizationUpdateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AuthorizationUpdateResponse>> CreateAuthorizationsAsync([FromBody] AuthorizationUpdateRequest request)
        {
            var validationResult = ValidateRequest(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            var response = InitializeResponse(request);

            _logger.LogInformation("Starting authorization workflow for ContactId: {ContactId}, AccountId: {AccountId}",
                request.Authorization.ContactId,
                request.Authorization.AccountId);

            var hasPennylanePermission = request.PermissionsCodes.Contains(PermissionCodes.PennylaneAccess);

            if (hasPennylanePermission)
            {
                var existingAccessResult = await CheckExistingPennylaneAccessAsync(request, response);
                if (existingAccessResult != null)
                {
                    return existingAccessResult;
                }

                var pennylaneProvisioningResult = await HandlePennylaneProvisioningAsync(request, response);
                if (pennylaneProvisioningResult != null)
                {
                    return pennylaneProvisioningResult;
                }
            }

            var authorizationUpdateResult = await UpdateAuthorizationsAsync(request, response);
            if (authorizationUpdateResult != null)
            {
                return authorizationUpdateResult;
            }

            return Ok(response);
        }

        private ActionResult<AuthorizationUpdateResponse>? ValidateRequest(AuthorizationUpdateRequest request)
        {
            if (request?.PermissionsCodes == null)
            {
                _logger.LogWarning("Authorization request is invalid or missing required data.");

                return BadRequest(new ErrorResponse
                {
                    ErrorCode = Errors.NotValidObjectCode,
                    ErrorMessage = string.Format(
                        Errors.NotValidObjectMessage,
                        nameof(AuthorizationUpdateRequest),
                        nameof(CreateAuthorizationsAsync))
                });
            }

            return null;
        }

        private static AuthorizationUpdateResponse InitializeResponse(AuthorizationUpdateRequest request)
        {
            return new AuthorizationUpdateResponse
            {
                ContactId = request.Authorization.ContactId,
                AccountId = request.Authorization.AccountId
            };
        }

        private async Task<ActionResult<AuthorizationUpdateResponse>?> CheckExistingPennylaneAccessAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            var existingAuthorizations = await _authorizationService.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId) ?? [];

            if (existingAuthorizations.Contains(PermissionCodes.PennylaneAccess))
            {
                _logger.LogInformation("ContactId: {ContactId} already has Pennylane access on AccountId: {AccountId}. Skipping provisioning and update.",
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                response.ProvisioningResult = new AccessProvisioningResult
                {
                    Status = PennylaneAccessStatuses.AlreadyHasAccess,
                    Message = "Already has Pennylane access",
                    ContactId = request.Authorization.ContactId,
                    AccountId = request.Authorization.AccountId,
                    Role = request.Authorization.Role
                };
                response.AuthorizationUpdated = true;

                return Ok(response);
            }

            return null;
        }

        private async Task<ActionResult<AuthorizationUpdateResponse>?> HandlePennylaneProvisioningAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            if (request.Authorization == null || string.IsNullOrWhiteSpace(request.Authorization.Role))
            {
                _logger.LogWarning("Pennylane Authorization request is invalid or missing required data.");
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = Errors.NotValidObjectCode,
                    ErrorMessage = string.Format(
                        Errors.NotValidObjectMessage,
                        nameof(AuthorizationUpdateRequest),
                        nameof(CreateAuthorizationsAsync))
                });
            }

            try
            {
                _logger.LogInformation("Granting Pennylane access for ContactId: {ContactId}, AccountId: {AccountId}, Role: {Role}",
                    request.Authorization.ContactId,
                    request.Authorization.AccountId,
                    request.Authorization.Role);

                var pennylaneRequest = new PennylaneAuthorizationRequest
                {
                    AccountId = request.Authorization.AccountId,
                    ContactId = request.Authorization.ContactId,
                    Role = request.Authorization.Role
                };

                var pennylaneResult = await _pennylaneService.GrantPennylaneAccessAsync(pennylaneRequest);
                response.ProvisioningResult = MapToProvisioningResult(pennylaneResult);
                response.ProvisioningStepCompleted = true;

                _logger.LogInformation("Pennylane access grant returned status {Status} for ContactId: {ContactId}, AccountId: {AccountId}",
                    pennylaneResult.Status,
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                if (pennylaneResult.Status.Equals(PennylaneAccessStatuses.Failed, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Pennylane access grant failed for ContactId: {ContactId}, AccountId: {AccountId}", request.Authorization.ContactId, request.Authorization.AccountId);
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = Errors.PennylaneHttpRequestFailedCode,
                        ErrorMessage = string.Format(
                            Errors.PennylaneHttpRequestFailedMessage,
                            nameof(AuthorizationUpdateRequest),
                            nameof(CreateAuthorizationsAsync))
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Downstream request failed while creating Pennylane authorizations for ContactId: {ContactId}, AccountId: {AccountId}",
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = Errors.BadRequestDownstreamCode,
                    ErrorMessage = Errors.BadRequestDownstreamMessage
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Unexpected error while creating Pennylane authorizations for ContactId: {ContactId}, AccountId: {AccountId}",
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    ErrorCode = Errors.UnexpectedExceptionCode,
                    ErrorMessage = string.Format(Errors.UnexptectedExceptionMessage, ex.Message)
                });
            }

            return null;
        }

        private async Task<ActionResult<AuthorizationUpdateResponse>?> UpdateAuthorizationsAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            try
            {
                response.AuthorizationUpdated = await _authorizationService.CreateOrUpdateContactAccountAuthorizationAsync(
                    request.Authorization.ContactId,
                    request.Authorization.AccountId,
                    request.PermissionsCodes);

                if (!response.AuthorizationUpdated)
                {
                    _logger.LogWarning("Authorization update returned false for ContactId: {ContactId}, AccountId: {AccountId}",
                        request.Authorization.ContactId,
                        request.Authorization.AccountId);

                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = Errors.UnexpectedExceptionCode,
                        ErrorMessage = string.Format(
                            Errors.UnexptectedExceptionMessage,
                            nameof(CreateAuthorizationsAsync))
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Downstream request failed while creating authorizations for ContactId: {ContactId}, AccountId: {AccountId}",
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = Errors.BadRequestDownstreamCode,
                    ErrorMessage = Errors.BadRequestDownstreamMessage
                });
            }

            return null;
        }

        private static AccessProvisioningResult MapToProvisioningResult(GrantPennylaneAccessResult result)
        {
            return new AccessProvisioningResult
            {
                Status = result.Status,
                Message = result.Message,
                ContactId = result.ContactId,
                AccountId = result.AccountId,
                ExternalUserId = result.PennylaneUserId,
                ExternalCompanyId = result.PennylaneCompanyId,
                Role = result.Role,
                Error = result.Error
            };
        }
    }
}
