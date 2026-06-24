using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.ProspectExperience.Controllers;

[Route("gtw/prospect/api/onboarding")]
[ApiController]
[Authorize]
public class ProspectExperienceController(
    IUserContext userContext,
    IFeatureFlagService featureFlagService,
    IIdentityService identityService,
    IProspectService prospectService,
    IValidator<CreateProspectRequest> createProspectValidator,
    IContactService contactService,
    ICommercialProposalOrchestrationService commercialProposalOrchestrationService,
    IEngagementLetterOrchestrationService engagementLetterOrchestrationService,
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

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, context: FeatureContext.FromEmail(userEmail), ct: ct))
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

    [HttpPut("{prospectId}/steps/complete")]
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

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, context: FeatureContext.FromEmail(userEmail), ct: ct))
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

    private const string PdfContentType = "application/pdf";
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private IActionResult? ValidatePdfFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse
            {
                ErrorCode = Errors.InvalidRequestCode,
                ErrorMessage = "A non-empty file is required."
            });

        if (!string.Equals(file.ContentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = "Only PDF files are accepted." });

        if (file.Length > MaxFileSizeBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "File size must not exceed 5 MB." });

        return null;
    }

    [HttpPost("{prospectId}/commercial-proposal")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendCommercialProposalAsync(
        int prospectId,
        IFormFile file,
        CancellationToken ct)
    {
        if (ValidatePdfFile(file) is { } fileError)
            return fileError;

        var userEmail = userContext.User.GetEmail();
        var contact = await contactService.GetContactAsync(userEmail);

        if (contact is null)
        {
            logger.LogWarning("Contact not found for email {UserEmail} on commercial proposal send for prospect {ProspectId}", userEmail, prospectId);
            return NotFound();
        }

        var outcome = await commercialProposalOrchestrationService.SendAsync(prospectId, contact.Id, userEmail, file, ct);

        return outcome switch
        {
            CommercialProposalOrchestrationOutcome.Sent => StatusCode(StatusCodes.Status201Created),
            CommercialProposalOrchestrationOutcome.ProspectNotFound => NotFound(),
            CommercialProposalOrchestrationOutcome.AccountNumberNotFound => NotFound(),
            CommercialProposalOrchestrationOutcome.AlreadySent => Conflict(new { error = "The commercial proposal has already been sent." }),
            CommercialProposalOrchestrationOutcome.NotEligible => StatusCode(StatusCodes.Status422UnprocessableEntity, new { error = "The current user is not eligible to send a commercial proposal." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{prospectId}/engagement-letter")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendEngagementLetterAsync(
        int prospectId,
        IFormFile file,
        CancellationToken ct)
    {
        if (ValidatePdfFile(file) is { } fileError)
            return fileError;

        var userEmail = userContext.User.GetEmail();
        var contact = await contactService.GetContactAsync(userEmail);

        if (contact is null)
        {
            logger.LogWarning("Contact not found for email {UserEmail} on engagement letter send for prospect {ProspectId}", userEmail, prospectId);
            return NotFound();
        }

        var outcome = await engagementLetterOrchestrationService.SendAsync(prospectId, contact.Id, userEmail, file, ct);

        return outcome switch
        {
            EngagementLetterOrchestrationOutcome.Sent => StatusCode(StatusCodes.Status201Created),
            EngagementLetterOrchestrationOutcome.ProspectNotFound => NotFound(),
            EngagementLetterOrchestrationOutcome.AccountNumberNotFound => NotFound(),
            EngagementLetterOrchestrationOutcome.AlreadySent => Conflict(new { error = "The engagement letter has already been sent." }),
            EngagementLetterOrchestrationOutcome.NotEligible => StatusCode(StatusCodes.Status422UnprocessableEntity, new { error = "The current user is not eligible to send an engagement letter." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
