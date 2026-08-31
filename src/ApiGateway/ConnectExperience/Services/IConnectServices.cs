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
        /// </summary>
        /// <param name="contactId">The contact identifier, resolved from the token.</param>
        /// <returns><c>true</c> when the modal must be displayed.</returns>
        Task<bool> GetShouldDisplaySerenityModalAsync(int contactId);

        /// <summary>
        /// Persists the contact's Serenity choice. The choice is immutable.
        /// </summary>
        /// <param name="contactId">The contact identifier, resolved from the token.</param>
        /// <param name="isAccepted">The expressed choice.</param>
        /// <returns>A task.</returns>
        Task SetSerenityModalChoiceAsync(int contactId, bool isAccepted);
    }
}
