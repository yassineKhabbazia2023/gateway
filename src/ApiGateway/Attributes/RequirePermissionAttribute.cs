using ApiGateway.Authorization;
using ApiGateway.Constants;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ApiGateway.Attributes;

/// <summary>
/// Authorization attribute that validates user permissions against required permissions
/// Uses the shared PermissionValidationService for consistent authorization logic
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissions;
    private readonly PermissionLogic _logic;
    private readonly bool _checkAccountRole;

    /// <summary>
    /// Creates a permission requirement with OR logic (user needs at least one permission)
    /// </summary>
    public RequirePermissionAttribute(params string[] permissions)
        : this(PermissionLogic.Or, permissions)
    {
    }

    /// <summary>
    /// Creates a permission requirement with specified logic
    /// </summary>
    /// <param name="logic">AND = user needs all permissions, OR = user needs at least one</param>
    /// <param name="permissions">Required permission codes</param>
    public RequirePermissionAttribute(PermissionLogic logic, params string[] permissions)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _logic = logic;
        _checkAccountRole = true; // Default behavior: check account role

        if (_permissions.Length == 0)
        {
            throw new ArgumentException("Au moins une permission doit être spécifiée", nameof(permissions));
        }
    }

    /// <summary>
    /// Whether to check if user has a role on the account (default: true)
    /// Set to false for endpoints that only need permission checks
    /// </summary>
    public bool CheckAccountRole
    {
        get => _checkAccountRole;
        init => _checkAccountRole = value;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetService<ILogger<RequirePermissionAttribute>>();

        try
        {
            var endpoint = httpContext.Request.Path;
            var method = httpContext.Request.Method;
            var permissionsRequired = string.Join(", ", _permissions);

            logger?.LogInformation(
                "Authorization check started for {Method} {Endpoint}. Required permissions ({Logic}): {Permissions}",
                method, endpoint, _logic, permissionsRequired);

            // Get the validation service
            var validationService = httpContext.RequestServices
                .GetRequiredService<IPermissionValidationService>();

            // Extract account ID from request (if present)
            var accountId = ExtractAccountId(httpContext);
            if (accountId.HasValue)
            {
                logger?.LogInformation("AccountId extracted from request: {AccountId}", accountId.Value);
            }
            else
            {
                logger?.LogInformation("No AccountId found in request (query/path/header)");
            }

            // Extract user email and get contact ID
            var userEmail = ExtractUserEmail(httpContext);
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new GatewayException(
                    StatusCodes.Status400BadRequest,
                    Errors.NullArgumentCode,
                    string.Format(Errors.NullArgumentMessage, nameof(userEmail)));
            }

            logger?.LogInformation("User email extracted: {UserEmail}", userEmail);

            // Get contact ID - this is REQUIRED for authorization
            var contactService = httpContext.RequestServices.GetRequiredService<IContactService>();
            var contactId = await contactService.GetContactIdAsync(userEmail);

            if (string.IsNullOrWhiteSpace(contactId))
            {
                throw new GatewayException(
                    StatusCodes.Status400BadRequest,
                    Errors.NullArgumentCode,
                    string.Format(Errors.NullArgumentMessage, nameof(contactId)));
            }

            logger?.LogInformation("ContactId retrieved: {ContactId} for user {UserEmail}", contactId, userEmail);

            // For AND logic, we need to check each permission individually
            // For OR logic, we pass all permissions and check if user has at least one
            if (_logic == PermissionLogic.And)
            {
                logger?.LogInformation("Checking permissions with AND logic (user needs ALL permissions)");

                // Validate each permission individually (AND logic)
                foreach (var permission in _permissions)
                {
                    await validationService.ValidatePermissionsAsync(
                        contactId,
                        new[] { permission },
                        accountId);

                    logger?.LogInformation("Permission {Permission} validated successfully for ContactId: {ContactId}",
                        permission, contactId);
                }

                logger?.LogInformation("All permissions validated successfully (AND logic)");
            }
            else
            {
                logger?.LogInformation("Checking permissions with OR logic (user needs at least ONE permission)");

                // Validate with OR logic (user needs at least one permission)
                await validationService.ValidatePermissionsAsync(
                    contactId,
                    _permissions,
                    accountId);

                logger?.LogInformation("Permission validation successful (OR logic) for ContactId: {ContactId}", contactId);
            }

            // If account role check is enabled and we have an account ID, validate role
            if (_checkAccountRole && accountId.HasValue)
            {
                logger?.LogInformation(
                    "Account role check enabled. Validating that ContactId {ContactId} has a role on AccountId {AccountId}",
                    contactId, accountId.Value);

                await validationService.ValidateAccountRoleAsync(
                    httpContext,
                    int.Parse(contactId),
                    accountId);

                logger?.LogInformation(
                    "Account role validation successful: ContactId {ContactId} has a role on AccountId {AccountId}",
                    contactId, accountId.Value);
            }
            else if (_checkAccountRole && !accountId.HasValue)
            {
                logger?.LogInformation("Account role check enabled but no AccountId present - skipping role validation");
            }
            else
            {
                logger?.LogInformation("Account role check disabled for this endpoint");
            }

            logger?.LogInformation(
                "Authorization successful for {Method} {Endpoint}. User: {UserEmail}, ContactId: {ContactId}, AccountId: {AccountId}",
                method, endpoint, userEmail, contactId, accountId?.ToString() ?? "N/A");
        }
        catch (GatewayException ex)
        {
            logger?.LogWarning(ex,
                "Authorization failed for {Method} {Endpoint}. StatusCode: {StatusCode}, Message: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, ex.StatusCode, ex.Message);

            // Convert GatewayException to appropriate MVC result
            context.Result = ex.StatusCode switch
            {
                StatusCodes.Status403Forbidden => BuildForbiddenResult(ex),
                StatusCodes.Status401Unauthorized => new UnauthorizedResult(),
                _ => new ObjectResult(new { error = ex.Message })
                {
                    StatusCode = ex.StatusCode
                }
            };
        }
        catch (Exception ex)
        {
            // Log unexpected errors
            logger?.LogError(ex,
                "Unexpected error during authorization for {Method} {Endpoint}",
                httpContext.Request.Method, httpContext.Request.Path);

            context.Result = new ObjectResult(new { error = "Authorization failed" })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    /// <summary>
    /// Builds the Gateway forbidden response without invoking an ASP.NET authentication forbid scheme.
    /// </summary>
    /// <param name="exception">The Gateway authorization exception.</param>
    /// <returns>The 403 response returned to the caller.</returns>
    private static ObjectResult BuildForbiddenResult(GatewayException exception)
    {
        return new ObjectResult(new
        {
            ErrorCode = exception.Code,
            ErrorMessage = exception.Message
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    /// <summary>
    /// Extracts user email from JWT token in the request
    /// </summary>
    private static string ExtractUserEmail(HttpContext httpContext)
    {
        var token = JwtHelper.ExtractBearerToken(httpContext.Request);

        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var userEmail = JwtHelper.ExtractUserEmailFromToken(token);

        return string.IsNullOrWhiteSpace(userEmail) ? string.Empty : userEmail;
    }

    /// <summary>
    /// Extracts account ID from route values, query parameters, URL path, or headers
    /// </summary>
    private static int? ExtractAccountId(HttpContext httpContext)
    {
        // Check if this endpoint should skip account check
        if (Array.Exists(GlobalsConstants.NoAccountCheckEndpoints,
            e => httpContext.Request.Path.ToString().Contains(e)))
        {
            return null;
        }

        // Try the route value first (e.g. "{accountId}" segments such as /onboarding/{accountId}/...)
        if (int.TryParse(httpContext.GetRouteValue("accountId")?.ToString(), out var parsedRouteAccountId))
        {
            return parsedRouteAccountId;
        }

        // Try query parameter first
        var accountIdParam = (string?)httpContext.Request.Query["accountId"];

        // Try URL path pattern /accounts/{id}
        if (string.IsNullOrWhiteSpace(accountIdParam))
        {
            const string pattern = @"/accounts/(\d+)";
            Match match = Regex.Match(
                httpContext.Request.Path,
                pattern,
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100));

            if (match.Success)
            {
                accountIdParam = match.Groups[1].Value;
            }
        }

        // Try header
        if (string.IsNullOrWhiteSpace(accountIdParam))
        {
            accountIdParam = httpContext.Request.Headers[GlobalsConstants.AccountIdHeader];
        }

        if (int.TryParse(accountIdParam, out var accountId))
        {
            return accountId;
        }

        return null;
    }
}

public enum PermissionLogic
{
    Or,  // Au moins une permission (par défaut)
    And  // Toutes les permissions requises
}
