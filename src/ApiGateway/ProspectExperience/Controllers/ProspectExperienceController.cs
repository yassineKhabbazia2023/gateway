using ApiGateway.Attributes;
using ApiGateway.Authorization.Consts;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Identity;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using ApiGateway.ProspectExperience.Helpers;
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
    IProspectApiClient prospectApiClient,
    IValidator<CreateProspectRequest> createProspectValidator,
    IContactService contactService,
    ICommercialProposalOrchestrationService commercialProposalOrchestrationService,
    IEngagementLetterOrchestrationService engagementLetterOrchestrationService,
    ILogger<ProspectExperienceController> logger) : ControllerBase
{
    [HttpPost("currentuser")]
    [ProducesResponseType(typeof(ProspectListItem), StatusCodes.Status201Created)]
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
            var fieldErrors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());

            logger.LogError(
                "Validation failed for prospect-creation endpoint. ServiceName: {ServiceName}, OperationName: {OperationName}, Route: {Route}, Siret: {Siret}, ValidationErrors: {@ValidationErrors}",
                "Pulse.Back.Gateway",
                nameof(CreateProspect),
                HttpContext.Request.Path.Value,
                request.Siret,
                fieldErrors);

            var detailedMessage = string.Join(
                " ",
                validationResult.Errors.Select(error => error.ErrorMessage).Distinct());

            var errorCode = validationResult.Errors.Any(error => error.ErrorCode == Errors.ProspectSignatoryEmailDomainForbiddenCode)
                ? Errors.ProspectSignatoryEmailDomainForbiddenCode
                : Errors.ProspectBadRequestCode;

            return StatusCode(StatusCodes.Status422UnprocessableEntity, new ErrorResponse
            {
                ErrorCode = errorCode,
                ErrorMessage = $"{Errors.ProspectBadRequestMessage} {detailedMessage}"
            });
        }

        var userEmail = userContext.User.GetEmail();
        if (!userContext.User.IsCollaborator() || !identityService.ValidateCollaborator(HttpContext))
        {
            logger.LogWarning(
                "[Response]: 403 - [Controller]: ProspectExperienceController - [Function]: Create Prospect - [Reason]: Collaborator access denied");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.NotValidCollaboratorCode,
                    ErrorMessage = string.Format(Errors.NotValidCollaboratorMessage, userEmail)
                });
        }
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

    [HttpPut("{accountId}/steps/complete")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentUploadResultResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentUploadResultResponse>> CompleteStepAsync(
        int accountId,
        [FromBody] CompleteStepRequest? request,
        CancellationToken ct)
    {
        if (accountId <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "AccountId must be greater than zero.");
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

        var authorization = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(accountId, userEmail, contactService, HttpContext);
        if (authorization.Error is not null)
        {
            return authorization.Error;
        }

        var result = await prospectService.CompleteStepAsync(accountId, request, ct);
        return Ok(result);
    }

    [HttpPost("{accountId}/supporting-documents/upload")]
    [Consumes("multipart/form-data")]
    [RequirePermission(
        PermissionCodes.ProspectOnboardingCollaboratorAccess,
        PermissionCodes.ProspectDocumentConsultationClientAccess,
        PermissionCodes.ProspectConfigurationConsultationClientAccess,
        CheckAccountRole = false)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadSupportingDocumentAsync(
        int accountId,
        [FromForm] string documentType,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        if (accountId <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "AccountId must be greater than zero.");
        }

        var userEmail = userContext.User.GetEmail();

        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, context: FeatureContext.FromEmail(userEmail), ct: ct))
        {
            logger.LogWarning("[Response]: 403 - [Controller]: ProspectExperienceController - [Function]: UploadSupportingDocument - [Reason]: Prospect experience is disabled by feature flag");
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    ErrorCode = Errors.ProspectExperienceDisabledCode,
                    ErrorMessage = Errors.ProspectExperienceDisabledMessage
                });
        }

        var authorization = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(accountId, userEmail, contactService, HttpContext);
        if (authorization.Error is not null)
        {
            return authorization.Error;
        }

        var contact = authorization.Contact;
        if (contact is null)
        {
            logger.LogWarning("Contact not found for email {UserEmail} on supporting document upload for account {AccountId}", userEmail, accountId);
            return NotFound();
        }

        var prospectId = await prospectApiClient.GetProspectIdByAccountIdAsync(accountId, ct);
        if (!prospectId.HasValue)
        {
            logger.LogWarning("No active prospect found for account {AccountId}", accountId);
            return NotFound();
        }

        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = documentType,
            File = file
        };

        try
        {
            await prospectService.UploadSupportingDocumentAsync(accountId, prospectId.Value, contact.Id, userEmail, request, ct);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (GatewayException ex)
        {
            return StatusCode(ex.StatusCode, new { errorCode = ex.ErrorCode, errorMessage = ex.Message });
        }
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

    [HttpPost("{accountId}/commercial-proposal")]
    [Consumes("multipart/form-data")]
    [RequirePermission(PermissionCodes.ProspectOnboardingCollaboratorAccess, PermissionCodes.CommercialProposalClientAccess, CheckAccountRole = false)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendCommercialProposalAsync(
        int accountId,
        IFormFile file,
        CancellationToken ct)
    {
        if (ValidatePdfFile(file) is { } fileError)
            return fileError;

        var userEmail = userContext.User.GetEmail();
        var authorization = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(accountId, userEmail, contactService, HttpContext);
        if (authorization.Error is not null)
        {
            return authorization.Error;
        }

        var contact = authorization.Contact;

        if (contact is null)
        {
            logger.LogWarning("Contact not found for email {UserEmail} on commercial proposal send for account {AccountId}", userEmail, accountId);
            return NotFound();
        }

        var outcome = await commercialProposalOrchestrationService.SendAsync(accountId, contact.Id, userEmail, file, ct);

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

    [HttpPost("{accountId}/engagement-letter")]
    [Consumes("multipart/form-data")]
    [RequirePermission(PermissionCodes.ProspectOnboardingCollaboratorAccess, PermissionCodes.EngagementLetterClientAccess, CheckAccountRole = false)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendEngagementLetterAsync(
        int accountId,
        IFormFile file,
        CancellationToken ct)
    {
        if (ValidatePdfFile(file) is { } fileError)
            return fileError;

        var userEmail = userContext.User.GetEmail();
        var authorization = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(accountId, userEmail, contactService, HttpContext);
        if (authorization.Error is not null)
        {
            return authorization.Error;
        }

        var contact = authorization.Contact;

        if (contact is null)
        {
            logger.LogWarning("Contact not found for email {UserEmail} on engagement letter send for account {AccountId}", userEmail, accountId);
            return NotFound();
        }

        var outcome = await engagementLetterOrchestrationService.SendAsync(accountId, contact.Id, userEmail, file, ct);

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
