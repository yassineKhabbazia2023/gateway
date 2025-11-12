using ApiGateway.Constants;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Authorization;

/// <summary>
/// Service for validating user permissions and authorization
/// Clean architecture with dependency injection - no HttpContext manipulation
/// </summary>
public class PermissionValidationService : IPermissionValidationService
{
    private readonly IAuthorizationService _authorizationService;

    public PermissionValidationService(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }
    /// <summary>
    /// Validates that the user has at least one of the required permissions
    /// Clean method: caller provides contactId, no HTTP context manipulation
    /// </summary>
    public async Task<bool> ValidatePermissionsAsync(
        string contactId,
        string[] requiredPermissions,
        int? accountId = null)
    {
        // Validate input parameters
        if (string.IsNullOrWhiteSpace(contactId))
        {
            throw new ArgumentNullException(nameof(contactId), "ContactId is required for permission validation");
        }

        if (requiredPermissions == null || requiredPermissions.Length == 0)
        {
            return true; // No permissions required
        }

        // Get user permissions
        var userPermissions = await _authorizationService.GetAllContactAuthorizationAsync(
            int.Parse(contactId),
            accountId) ?? [];

        // Check if user has at least one of the required permissions (OR logic)
        var hasPermission = userPermissions.Any(p =>
            requiredPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));

        if (!hasPermission)
        {
            throw new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.PermissionRequiredCode,
                Errors.PermissionRequiredMessage);
        }

        return true;
    }

    /// <summary>
    /// Validates that the user has a role on the specified account
    /// Logic extracted from AuthorizationMiddleware authorization flow
    /// </summary>
    public async Task<bool> ValidateAccountRoleAsync(
        HttpContext httpContext,
        int contactId,
        int? accountId)
    {
        if (!accountId.HasValue)
        {
            return true; // No account specified, skip role check
        }

        // Check if user has permissions that skip role check
        if (await AuthorizationHelper.SkipRoleCheck(accountId, contactId, httpContext))
        {
            return true;
        }

        // Validate user has role on account
        var hasRoleOnAccount = await AuthorizationHelper.CheckRoles(
            accountId,
            null,
            contactId,
            httpContext);

        if (!hasRoleOnAccount)
        {
            throw new GatewayException(
                StatusCodes.Status403Forbidden,
                Errors.RoleRequiredCode,
                string.Format(Errors.RoleRequiredMessage, contactId, accountId));
        }

        return true;
    }
}
