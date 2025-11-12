namespace ApiGateway.Authorization;

/// <summary>
/// Service for validating user permissions and authorization
/// </summary>
public interface IPermissionValidationService
{
    /// <summary>
    /// Validates that the user has at least one of the required permissions
    /// </summary>
    /// <param name="contactId">Contact ID (REQUIRED - caller must provide it)</param>
    /// <param name="requiredPermissions">List of permissions (OR logic - user needs at least one)</param>
    /// <param name="accountId">Optional account ID for account-level authorization</param>
    /// <returns>True if validation succeeds</returns>
    /// <exception cref="GatewayException">Thrown when validation fails</exception>
    Task<bool> ValidatePermissionsAsync(string contactId, string[] requiredPermissions, int? accountId = null);


    /// <summary>
    /// Validates that the user has a role on the specified account
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="contactId">The contact ID</param>
    /// <param name="accountId">The account ID</param>
    /// <returns>True if user has a role on the account</returns>
    /// <exception cref="GatewayException">Thrown when validation fails</exception>
    Task<bool> ValidateAccountRoleAsync(HttpContext httpContext, int contactId, int? accountId);
}
