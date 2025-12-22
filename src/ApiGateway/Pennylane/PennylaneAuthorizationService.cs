using System;
using System.Collections.Generic;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Consts;
using ApiGateway.Authorization.Mappers;
using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane.Mappers;
using ApiGateway.Pennylane.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Pennylane
{
    public class PennylaneAuthorizationService : IPennylaneAuthorizationService
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IPennylaneService _pennylaneService;
        private readonly ILogger<PennylaneAuthorizationService> _logger;

        public PennylaneAuthorizationService(
            IAuthorizationService authorizationService,
            IPennylaneService pennylaneService,
            ILogger<PennylaneAuthorizationService> logger)
        {
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _pennylaneService = pennylaneService ?? throw new ArgumentNullException(nameof(pennylaneService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HasPennylaneAccessAsync(AuthorizationUpdateRequest request)
        {
            var existingAuthorizations = await _authorizationService.GetContactAuthorizationAsync(
                request.Authorization!.ContactId,
                request.Authorization.AccountId) ?? new List<string>();

            return existingAuthorizations.Contains(PermissionCodes.PennylaneAccess);
        }

        public async Task<OperationResult> TryUpdatePennylaneRoleAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            try
            {
                var pennylaneRequest = new PennylaneAuthorizationRequest
                {
                    AccountId = request.Authorization!.AccountId,
                    ContactId = request.Authorization.ContactId,
                    Role = request.Authorization.Role!
                };

                var updateResult = await _pennylaneService.UpdatePennylaneRoleAsync(pennylaneRequest);
                response.ProvisioningResult = AccessProvisioningMapper.MapToProvisioningResult(updateResult);
                response.ProvisioningStepCompleted = true;
                response.AuthorizationUpdated = true;

                return OperationResult.Ok();
            }
            catch (PennylaneApiException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapPennylaneApiException(
                    _logger,
                    ex,
                    request,
                    "Downstream request failed while updating Pennylane role for ContactId: {ContactId}, AccountId: {AccountId}"));
            }
            catch (HttpRequestException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapHttpRequestException(
                    _logger,
                    ex,
                    request,
                    "Downstream request failed while updating Pennylane role for ContactId: {ContactId}, AccountId: {AccountId}"));
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapInvalidOperationException(
                    _logger,
                    ex,
                    request,
                    "Unexpected error while updating Pennylane role for ContactId: {ContactId}, AccountId: {AccountId}"));
            }
        }

        public async Task<OperationResult> TryHandlePennylaneProvisioningAsync(
            AuthorizationUpdateRequest request,
            AuthorizationUpdateResponse response)
        {
            try
            {
                _logger.LogInformation("Granting Pennylane access for ContactId: {ContactId}, AccountId: {AccountId}, Role: {Role}",
                    request.Authorization!.ContactId,
                    request.Authorization.AccountId,
                    request.Authorization.Role);

                var pennylaneRequest = new PennylaneAuthorizationRequest
                {
                    AccountId = request.Authorization.AccountId,
                    ContactId = request.Authorization.ContactId,
                    Role = request.Authorization.Role!
                };

                var pennylaneResult = await _pennylaneService.GrantPennylaneAccessAsync(pennylaneRequest);
                response.ProvisioningResult = AccessProvisioningMapper.MapToProvisioningResult(pennylaneResult);
                response.ProvisioningStepCompleted = true;

                _logger.LogInformation("Pennylane access grant returned status {Status} for ContactId: {ContactId}, AccountId: {AccountId}",
                    pennylaneResult.Status,
                    request.Authorization.ContactId,
                    request.Authorization.AccountId);

                if (pennylaneResult.Status.Equals(PennylaneAccessStatuses.Failed, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Pennylane access grant failed for ContactId: {ContactId}, AccountId: {AccountId}",
                        request.Authorization.ContactId,
                        request.Authorization.AccountId);
                    return OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse
                    {
                        ErrorCode = Errors.PennylaneHttpRequestFailedCode,
                        ErrorMessage = string.Format(
                            Errors.PennylaneHttpRequestFailedMessage,
                            nameof(AuthorizationUpdateRequest),
                            nameof(AuthorizationController.CreateOrUpdateAuthorizationsAsync))
                    }));
                }
            }
            catch (PennylaneApiException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapPennylaneApiException(
                    _logger,
                    ex,
                    request,
                    "Downstream request failed while creating Pennylane authorizations for ContactId: {ContactId}, AccountId: {AccountId}"));
            }
            catch (HttpRequestException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapHttpRequestException(
                    _logger,
                    ex,
                    request,
                    "Downstream request failed while creating Pennylane authorizations for ContactId: {ContactId}, AccountId: {AccountId}"));
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult.Fail(AuthorizationErrorMapper.MapInvalidOperationException(
                    _logger,
                    ex,
                    request,
                    "Unexpected error while creating Pennylane authorizations for ContactId: {ContactId}, AccountId: {AccountId}"));
            }

            return OperationResult.Ok();
        }
    }
}
