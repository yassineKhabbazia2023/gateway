using ApiGateway.Account;
using ApiGateway.Attributes;
using ApiGateway.Contact;
using ApiGateway.Contact.Enum;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.Offer.Constants;
using ApiGateway.Offer.Model;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Constants;
using ApiGateway.Pennylane.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("ApiGateway.UnitTests")]
namespace ApiGateway.Offer;

[Route("gtw/offer/api")]
[Authorize]
public class OfferController : ControllerBase
{
    private readonly ILogger<OfferController> _logger;
    private readonly IOfferService _offerService;
    private readonly IPennylaneService _pennylaneService;
    private readonly IAccountService _accountService;
    private readonly IContactService _contactService;
    private readonly IFeatureFlagService _featureFlagService;

    public OfferController(
        ILogger<OfferController> logger,
        IOfferService offerService,
        IPennylaneService pennylaneService,
        IAccountService accountService,
        IContactService contactService,
        IFeatureFlagService featureFlagService)
    {
        _logger = logger;
        _offerService = offerService;
        _pennylaneService = pennylaneService;
        _accountService = accountService;
        _contactService = contactService;
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Creates a new offer subscription
    /// Requires at least one of the specified permissions: COOFF001 (create offer), COOFF002 (manage offers), or COINFO001 (view info)
    /// Also validates that the user has a role on the specified account
    /// </summary>
    [HttpPost("subscription/create")]
    [RequirePermission("COOFF001")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<int>> CreateSubscription([FromBody] CreateSubscriptionOffer subscriptionRequest)
    {
        _logger.LogInformation("Creating subscription for AccountId: {AccountId}, OfferId: {OfferId}", subscriptionRequest.AccountId, subscriptionRequest.OfferId);
        _logger.LogDebug("Full request: {Request}", JsonSerializer.Serialize(subscriptionRequest));

        // Step 1: Check if we should create company in Pennylane for this OfferId
        if (_pennylaneService.ShouldCreateCompanyForOffer(subscriptionRequest.OfferId))
        {
            var contactsList = subscriptionRequest.Contacts ?? [];
            var contactTasks = contactsList.Select(id => _contactService.GetContactByIdAsync(id));
            var contacts = await Task.WhenAll(contactTasks);

            // Validation all signatory should have a mobilePhone
            var signatoryMissingMobilePhone = contacts.FirstOrDefault(c =>
                c != null
                && string.Equals(c.Type, ContactType.Customer.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(c.MobilePhone));

            if (signatoryMissingMobilePhone != null)
            {
                return BadRequest(new
                {
                    ErrorCode = Errors.MissingMobilePhoneCode,
                    ErrorMessage = string.Format(Errors.MissingMobilePhoneMessage, signatoryMissingMobilePhone.Id)
                });
            }

            string? companyStatus = null;
            string? requestedPlanCode = null;
            try
            {
                var account = await _accountService.GetAccountAsync(subscriptionRequest.AccountId);

                var accountingType = account?.Accounting?.AccountingType ?? string.Empty;
                var siren = account?.Legal?.Siren ?? string.Empty;
                var countryCode = account?.Address?.FirstOrDefault()?.Country ?? string.Empty;

                var notYetRegistered = subscriptionRequest.HasNoSiren == true
                    || string.IsNullOrWhiteSpace(siren);

                string? userNumber;
                (requestedPlanCode, userNumber) = await GetPlanInfoAsync(subscriptionRequest);

                if (requestedPlanCode == OfferPlanCodes.ApprovedPlatform
                    && !await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, GetUserEmail()))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
                    {
                        ErrorCode = Errors.ApprovedPlatformDisabledCode,
                        ErrorMessage = Errors.ApprovedPlatformDisabledMessage
                    });
                }

                var companyCreateRequest = new CreateCompanyRequest
                {
                    AccountId = subscriptionRequest.AccountId,
                    Contacts = subscriptionRequest.Contacts,
                    ContactFunctions = subscriptionRequest.ContactFunctions,
                    HubName = subscriptionRequest.HubName,
                    RegistrationNumber = notYetRegistered ? null : siren,
                    NotYetRegistered = notYetRegistered,
                    AccountingType = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(accountingType),
                    CountryCode = CountryCodeMapper.CountryToPennylaneCountryCode(countryCode),
                    RequestedPlanCode = requestedPlanCode,
                    NumberOfUsers = userNumber,
                };

                var companyResult = await _pennylaneService.CreateCompanyAsync(companyCreateRequest);

                if (companyResult.Status == PennylaneControllerStatuses.RequiresFirmAssignment)
                {
                    return Conflict(new
                    {
                        ErrorCode = Errors.PennylaneFirmAssignmentRequiredCode,
                        ErrorMessage = Errors.PennylaneFirmAssignmentRequiredMessage
                    });
                }

                _logger.LogInformation("Pennylane company creation result: Status={Status}, AccountId={AccountId}",
                    companyResult.Status, companyCreateRequest.AccountId);
                companyStatus = companyResult.Status;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is BadHttpRequestException)
            {
                _logger.LogError(ex, "Pennylane API request failed for AccountId: {AccountId}. Proceeding with subscription creation.",
                    subscriptionRequest.AccountId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Pennylane response for AccountId: {AccountId}. Proceeding with subscription creation.",
                    subscriptionRequest.AccountId);
            }

            // Set status based on whether company was created
            subscriptionRequest.Status = companyStatus switch
            {
                PennylaneControllerStatuses.ToCreate => PennylaneConstants.PennylaneNotCreated,
                PennylaneControllerStatuses.Created
                    or PennylaneControllerStatuses.CreatedOnDefault
                    or PennylaneControllerStatuses.AlreadyExists
                    when requestedPlanCode == OfferPlanCodes.ApprovedPlatform
                        => PennylaneConstants.PennylaneMandateToSend,
                PennylaneControllerStatuses.Created => PennylaneConstants.PennylaneCreated,
                PennylaneControllerStatuses.Validated => PennylaneControllerStatuses.Validated,
                _ => PennylaneConstants.PennylaneToVerify
            };
        }

        // Step 2: Create subscription (continues even if company creation failed/skipped)
        var subscriptionId = await _offerService.CreateSubscriptionAsync(subscriptionRequest);
        return Ok(subscriptionId);
    }

    private string? GetUserEmail()
    {
        return User?.FindFirst("upn")?.Value ?? User?.FindFirst("email")?.Value;
    }

    internal async Task<Tuple<string?, string?>> GetPlanInfoAsync(CreateSubscriptionOffer subscriptionRequest)
    {
        string? requestedPlanCode = null;
        string? userNumber = null;

        if (subscriptionRequest.PlanId.HasValue)
        {
            var offer = await _offerService.GetOfferByIdAsync(subscriptionRequest.OfferId);
            requestedPlanCode = offer?.Plans?
                .FirstOrDefault(p => p.PlanId == subscriptionRequest.PlanId.Value)?
                .PlanCode;

            if (subscriptionRequest.PlanPricingId.HasValue)
            {
                userNumber = offer?.Plans?.FirstOrDefault(p => p.PlanCode == requestedPlanCode)?.Pricings?
                    .FirstOrDefault(p => p.PlanPricingId == subscriptionRequest.PlanPricingId)?.Label;
            }
        }

        return new Tuple<string?, string?>(requestedPlanCode, userNumber);
    }
}
