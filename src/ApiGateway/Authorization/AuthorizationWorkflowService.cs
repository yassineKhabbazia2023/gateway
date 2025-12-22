using ApiGateway.Authorization.Mappers;
using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Authorization
{
    public class AuthorizationWorkflowService : IAuthorizationWorkflowService
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<AuthorizationWorkflowService> _logger;

        public AuthorizationWorkflowService(
            IAuthorizationService authorizationService,
            ILogger<AuthorizationWorkflowService> logger)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationResult> TryUpdateAuthorizationsAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            try
            {
                response.AuthorizationUpdated = await _authorizationService.CreateOrUpdateContactAccountAuthorizationAsync(
                    request.Authorization!.ContactId,
                    request.Authorization.AccountId,
                    request.PermissionsCodes);

                if (!response.AuthorizationUpdated)
                {
                    _logger.LogWarning("Authorization update returned false for ContactId: {ContactId}, AccountId: {AccountId}",
                        request.Authorization.ContactId,
                        request.Authorization.AccountId);

                    return OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse
                    {
                        ErrorCode = Errors.UnexpectedExceptionCode,
                        ErrorMessage = string.Format(
                            Errors.UnexptectedExceptionMessage,
                            nameof(AuthorizationController.CreateOrUpdateAuthorizationsAsync))
                    }));
                }
            }
            catch (HttpRequestException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapHttpRequestException(
                    _logger,
                    ex,
                    request,
                    "Downstream request failed while creating authorizations for ContactId: {ContactId}, AccountId: {AccountId}"));
            }

            return OperationResult.Ok();
        }
    }
}
