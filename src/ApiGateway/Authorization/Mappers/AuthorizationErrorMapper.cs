using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Authorization.Mappers
{
    public static class AuthorizationErrorMapper
    {
        public static ActionResult<AuthorizationUpdateResponse> MapPennylaneApiException(
            ILogger logger,
            PennylaneApiException ex,
            AuthorizationUpdateRequest request,
            string logMessageTemplate)
        {
            logger.LogError(
                ex,
                logMessageTemplate,
                request.Authorization!.ContactId,
                request.Authorization.AccountId);

            return new ObjectResult(new ErrorResponse
            {
                ErrorCode = Errors.BadRequestDownstreamCode,
                ErrorMessage = Errors.BadRequestDownstreamMessage
            })
            {
                StatusCode = (int)ex.StatusCode
            };
        }

        public static ActionResult<AuthorizationUpdateResponse> MapHttpRequestException(
            ILogger logger,
            HttpRequestException ex,
            AuthorizationUpdateRequest request,
            string logMessageTemplate)
        {
            logger.LogError(
                ex,
                logMessageTemplate,
                request.Authorization!.ContactId,
                request.Authorization.AccountId);

            return new ObjectResult(new ErrorResponse
            {
                ErrorCode = Errors.BadRequestDownstreamCode,
                ErrorMessage = Errors.BadRequestDownstreamMessage
            })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }

        public static ActionResult<AuthorizationUpdateResponse> MapInvalidOperationException(
            ILogger logger,
            InvalidOperationException ex,
            AuthorizationUpdateRequest request,
            string logMessageTemplate)
        {
            logger.LogError(
                ex,
                logMessageTemplate,
                request.Authorization!.ContactId,
                request.Authorization.AccountId);

            return new ObjectResult(new ErrorResponse
            {
                ErrorCode = Errors.UnexpectedExceptionCode,
                ErrorMessage = string.Format(Errors.UnexptectedExceptionMessage, ex.Message)
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
