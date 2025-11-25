using ApiGateway.Account;
using ApiGateway.Attributes;
using ApiGateway.Offer.Model;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;
using System.Text.Json;

namespace ApiGateway.Offer
{
    [Route("gtw/offer/api")]
    [Authorize]
    public class OfferController : ControllerBase
    {
        private readonly ILogger<OfferController> _logger;
        private readonly IOfferService _offerService;
        private readonly IPennylaneService _pennylaneService;
        private readonly IAccountService _accountService;

        public OfferController(
            ILogger<OfferController> logger,
            IOfferService offerService,
            IPennylaneService pennylaneService,
            IAccountService accountService)
        {
            _logger = logger;
            _offerService = offerService;
            _pennylaneService = pennylaneService;
            _accountService = accountService;
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
        public async Task<ActionResult<int>> CreateSubscription([FromBody] CreateSubscriptionOffer subscriptionRequest)
        {
            _logger.LogInformation("Creating subscription for AccountId: {AccountId}, OfferId: {OfferId}", subscriptionRequest.AccountId, subscriptionRequest.OfferId);
            _logger.LogDebug("Full request: {Request}", JsonSerializer.Serialize(subscriptionRequest));

            // Step 1: Check if we should create company in Pennylane for this OfferId
            if (_pennylaneService.ShouldCreateCompanyForOffer(subscriptionRequest.OfferId))
            {
                try
                {
                    var account = await _accountService.GetAccountAsync(subscriptionRequest.AccountId);

                    var accountingType = account?.Accounting?.AccountingType ?? string.Empty;
                    var siren = account?.Legal?.Siren ?? string.Empty;
                    var countryCode = account?.Address?.FirstOrDefault()?.Country ?? string.Empty;

                    var companyCreateRequest = new CreateCompanyRequest
                    {
                        AccountId = subscriptionRequest.AccountId,
                        Contacts = subscriptionRequest.Contacts,
                        RegistrationNumber = siren,
                        NotYetRegistered = string.IsNullOrWhiteSpace(siren),
                        AccountingType = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(accountingType),
                        CountryCode = CountryCodeMapper.CountryToPennylaneCountryCode(countryCode),
                    };
            
                    var companyResult = await _pennylaneService.CreateCompanyAsync(companyCreateRequest);
                    _logger.LogInformation("Pennylane company creation result: Status={Status}, CompanyId={CompanyId}",
                        companyResult.Status, companyResult.Company.Id);
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

                // Continue with subscription creation even if company creation fails
            }

            // Step 2: Create subscription (continues even if company creation failed/skipped)
            var subscriptionId = await _offerService.CreateSubscriptionAsync(subscriptionRequest);

            return Ok(subscriptionId);
        }
    }
}
