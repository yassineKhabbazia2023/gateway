using ApiGateway.ConnectExperience.Models;
using ApiGateway.Models;

namespace ApiGateway.ConnectExperience.Services
{
    public interface IConnectServices
    {
        Task<UserInformation> GetUserInformation(string userEmail);

        Task<Summary?> GetSummaryAsync(int accountId, int currentUserId, string contactType);

        Task<IEnumerable<int>> SendEmailAsync(string userEmail, int accountId, int[] customerIDs, string? entityType = null);

        /// <summary>
        /// Decides whether the Serenity modal must be displayed to the given contact.
        /// True as soon as at least ONE entity of the contact's portfolio matches every criterion.
        /// Gated by the <see cref="FeatureFlags.FeatureFlagKeys.IsSerenityRedirectionModalEnabled"/> flag: throws
        /// <see cref="Pulse.ExceptionMiddleware.Exceptions.ForbiddenException"/> without evaluating the business
        /// criteria when the flag is disabled.
        /// </summary>
        /// <param name="contactId">The contact identifier, resolved from the token.</param>
        /// <param name="userEmail">The connected contact's email, used for feature flag targeting.</param>
        /// <returns><c>true</c> when the modal must be displayed.</returns>
        Task<bool> GetShouldDisplaySerenityModalAsync(int contactId, string userEmail);

        /// <summary>
        /// Persists the contact's Serenity choice. The choice is immutable.
        /// Gated by the <see cref="FeatureFlags.FeatureFlagKeys.IsSerenityRedirectionModalEnabled"/> flag: throws
        /// <see cref="Pulse.ExceptionMiddleware.Exceptions.ForbiddenException"/> when the flag is disabled.
        /// </summary>
        /// <param name="contactId">The contact identifier, resolved from the token.</param>
        /// <param name="userEmail">The connected contact's email, used for feature flag targeting.</param>
        /// <param name="isAccepted">The expressed choice.</param>
        /// <returns>A task.</returns>
        Task SetSerenityModalChoiceAsync(int contactId, string userEmail, bool isAccepted);
    }
}
