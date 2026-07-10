using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.ProspectExperience.Helpers;

/// <summary>
/// Provides Prospect account authorization helpers for Gateway controller endpoints.
/// </summary>
public static class ProspectAccountAuthorizationHelper
{
    /// <summary>
    /// Applies the same account role authorization checks used by existing Gateway account-scoped endpoints.
    /// </summary>
    /// <param name="accountId">The prospect account identifier.</param>
    /// <param name="contactEmail">The connected contact email.</param>
    /// <param name="contactService">The contact service used to resolve the connected contact.</param>
    /// <param name="httpContext">The current HTTP context containing request services for authorization checks.</param>
    /// <returns>The authorization error when denied; otherwise, the resolved contact.</returns>
    public static async Task<(ActionResult? Error, ApiGateway.Contact.Models.Contact? Contact)> AuthorizeAccountRoleAsync(
        int accountId,
        string contactEmail,
        IContactService contactService,
        HttpContext httpContext)
    {
        var contact = await contactService.GetContactAsync(contactEmail);
        if (contact is null)
        {
            return (new NotFoundResult(), null);
        }

        var shouldSkipRoleCheck = await AuthorizationHelper.SkipRoleCheck(accountId, contact.Id, httpContext);
        if (!shouldSkipRoleCheck)
        {
            var hasTheNeededRoles = await AuthorizationHelper.CheckRoles(accountId, null, contact.Id, httpContext);
            if (!hasTheNeededRoles)
            {
                return (new ObjectResult(new
                {
                    ErrorCode = Errors.NoRoleOnAccountCode,
                    ErrorMessage = string.Format(Errors.NoRoleOnAccountMessage, contact.Id, accountId)
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                }, null);
            }
        }

        return (null, contact);
    }
}
