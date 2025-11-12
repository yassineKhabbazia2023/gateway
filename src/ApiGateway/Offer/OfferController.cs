using ApiGateway.Attributes;
using ApiGateway.Offer.Model;
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

        public OfferController(ILogger<OfferController> logger, IOfferService offerService)
        {
            _logger = logger;
            _offerService = offerService;
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

            var subscriptionId = await _offerService.CreateSubscriptionAsync(subscriptionRequest);

            return Ok(subscriptionId);
        }
    }
}
