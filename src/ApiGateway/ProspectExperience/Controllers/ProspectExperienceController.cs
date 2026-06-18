using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.Identity;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.ProspectExperience.Controllers;

[Route("gtw/prospect/api/prospects")]
[ApiController]
[Authorize]
public class ProspectExperienceController(
    IUserContext userContext,
    IFeatureFlagService featureFlagService,
    IIdentityService identityService,
    IProspectService prospectService,
    IValidator<CreateProspectRequest> createProspectValidator,
    ILogger<ProspectExperienceController> logger) : ControllerBase
{
    [HttpPost("currentuser")]
    [ProducesResponseType(typeof(ProspectListItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProspectListItem>> CreateProspect(
        [FromBody] CreateProspectRequest request,
        CancellationToken ct)
    {
        var validationResult = await createProspectValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            logger.LogError(
                "Validation failed for prospect-creation endpoint. ServiceName: {ServiceName}, OperationName: {OperationName}, Route: {Route}, Siret: {Siret}, ValidationErrors: {@ValidationErrors}",
                "Pulse.Back.Gateway",
                nameof(CreateProspect),
                HttpContext.Request.Path.Value,
                request.Siret,
                validationResult.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()));
            return BadRequest(new ErrorResponse
            {
                ErrorCode = Errors.InvalidRequestCode,
                ErrorMessage = Errors.InvalidRequestMessage
            });
        }

        var userEmail = userContext.User.GetEmail();

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, userEmail, ct))
        {
            logger.LogWarning("[Response]: 403 - [Controller]: ProspectExperienceController - [Function]: CreateProspect - [Reason]: Prospect experience is disabled by feature flag");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.ProspectExperienceDisabledCode,
                    ErrorMessage = Errors.ProspectExperienceDisabledMessage
                });
        }

        var prospect = await prospectService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, prospect);
    }

    /// <summary>
    /// Completes a prospect onboarding step by orchestrating Prospect document retrieval and Registry uploads.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="request">The step completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The consolidated upload result.</returns>
    [HttpPut("{prospectId}/onboarding/steps/complete")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentUploadResultResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentUploadResultResponse>> CompleteStepAsync(
        int prospectId,
        [FromBody] CompleteStepRequest? request,
        CancellationToken ct)
    {
        if (prospectId <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "ProspectId must be greater than zero.");
        }

        var userEmail = userContext.User.GetEmail();

        if (!userContext.User.IsCollaborator() || !identityService.ValidateCollaborator(HttpContext))
        {
            logger.LogWarning(
                "[Response]: 403 - [Controller]: ProspectExperienceController - [Function]: CompleteStep - [Reason]: Collaborator access denied");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.NotValidCollaboratorCode,
                    ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, userEmail)
                });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.StepName))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = Errors.InvalidRequestCode,
                ErrorMessage = Errors.InvalidRequestMessage
            });
        }

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, userEmail, ct))
        {
            logger.LogWarning("[Response]: 403 - [Controller]: ProspectExperienceController - [Function]: CompleteStep - [Reason]: Prospect experience is disabled by feature flag");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.ProspectExperienceDisabledCode,
                    ErrorMessage = Errors.ProspectExperienceDisabledMessage
                });
        }

        var result = await prospectService.CompleteStepAsync(prospectId, request, ct);
        return Ok(result);
    }
}
