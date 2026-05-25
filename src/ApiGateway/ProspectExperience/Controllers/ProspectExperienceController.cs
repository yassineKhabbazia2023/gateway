using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
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
}
