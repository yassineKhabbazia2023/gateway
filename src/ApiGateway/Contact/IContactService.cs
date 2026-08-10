using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.Contact;

/// <summary>
/// Provides access to Contact downstream operations used by the Gateway.
/// </summary>
public interface IContactService
{
    /// <summary>
    /// Gets a contact identifier by user email.
    /// </summary>
    /// <param name="userEmail">The user email.</param>
    /// <returns>The contact identifier when found; otherwise, null.</returns>
    Task<string?> GetContactIdAsync(string userEmail);

    /// <summary>
    /// Gets a contact by user email.
    /// </summary>
    /// <param name="userEmail">The user email.</param>
    /// <returns>The contact when found; otherwise, null.</returns>
    Task<Models.Contact?> GetContactAsync(string userEmail);

    /// <summary>
    /// Gets a contact by identifier.
    /// </summary>
    /// <param name="contactId">The contact identifier.</param>
    /// <returns>The contact when found; otherwise, null.</returns>
    Task<Models.Contact?> GetContactByIdAsync(int contactId);

    /// <summary>
    /// Creates a contact for the Prospect experience.
    /// </summary>
    /// <param name="request">The contact creation request.</param>
    /// <param name="contactEmail">The optional current user email, forwarded as the ContactEmail header.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created contact result.</returns>
    Task<ContactCreated> CreateContactForProspectAsync(CreateContactRequest request, string? contactEmail, CancellationToken ct);

    /// <summary>
    /// Calls the Contact authentication endpoint to create a new password.
    /// </summary>
    /// <param name="request">The create-new-password request.</param>
    /// <param name="entityType">The optional entity type query value to send downstream.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The raw Contact downstream response.</returns>
    Task<HttpResponseMessage> CreateNewPasswordAsync(CreateNewPasswordRequest request, string? entityType, CancellationToken ct);

    Task<IEnumerable<int>> SendEmailAsync(int currentUserId, string accountNumber, int[] customerIDs, string? entityType = null);
}
