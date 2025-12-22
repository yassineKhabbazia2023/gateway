using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Authorization.Validators
{
    public class AuthorizationRequestValidator : IAuthorizationRequestValidator
    {
        private readonly ILogger<AuthorizationRequestValidator> _logger;

        public AuthorizationRequestValidator(ILogger<AuthorizationRequestValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryValidateRequest(AuthorizationUpdateRequest request, out ActionResult<AuthorizationUpdateResponse> errorResult)
        {
            if (request?.PermissionsCodes == null)
            {
                _logger.LogWarning("Authorization request is invalid or missing required data.");

                errorResult = new BadRequestObjectResult(new ErrorResponse
                {
                    ErrorCode = Errors.NotValidObjectCode,
                    ErrorMessage = string.Format(
                        Errors.NotValidObjectMessage,
                        nameof(AuthorizationUpdateRequest),
                        nameof(AuthorizationController.CreateOrUpdateAuthorizationsAsync))
                });

                return false;
            }

            errorResult = null!;
            return true;
        }

        public bool TryValidatePennylaneAuthorizationDetails(AuthorizationUpdateRequest request, out ActionResult<AuthorizationUpdateResponse> errorResult)
        {
            if (request.Authorization == null || string.IsNullOrWhiteSpace(request.Authorization.Role))
            {
                _logger.LogWarning("Authorization target is invalid or missing required data.");

                errorResult = new BadRequestObjectResult(new ErrorResponse
                {
                    ErrorCode = Errors.NotValidObjectCode,
                    ErrorMessage = string.Format(
                        Errors.NotValidObjectMessage,
                        nameof(AuthorizationUpdateRequest),
                        nameof(AuthorizationController.CreateOrUpdateAuthorizationsAsync))
                });

                return false;
            }

            errorResult = null!;
            return true;
        }
    }
}
