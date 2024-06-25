using ApiGateway.Authorization;
using Ocelot.Middleware;
using ApiGateway.Contact;
using ApiGateway.Helpers;
using Ocelot.Authorization;
using System.Text.RegularExpressions;
using ApiGateway.Account;
using ApiGateway.Constants;

namespace ApiGateway.Middlewares;

public static class AuthorizationMiddleware
{
    public static Func<HttpContext, Func<Task>, Task> AuthorizationFilter => async (httpContext, next) =>
    {
        var userEmail = ValidateUserIdentity(httpContext);
        var contactService = httpContext.RequestServices.GetRequiredService<IContactService>();
        var contactId = await contactService!.GetContactIdAsync(userEmail);
        var requiredClaims = ValidateRequireClaim(httpContext);
        int? accountId = ValidateAccountId(httpContext);

        if (requiredClaims.Count == 0 && !accountId.HasValue)
        {
            await next.Invoke();
            return;
        }

        if (!await CheckClaims(requiredClaims, userEmail, accountId, contactId, httpContext))
        {
            return;
        }

        if (!await CheckRoles(accountId, contactId, httpContext))
        {
            return;
        }

        await next.Invoke();
    };

    private static async Task<bool> CheckClaims(List<string> requiredClaims, string userEmail, int? accountId, string? contactId, HttpContext httpContext)
    {
        if (requiredClaims.Count != 0)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                ForbiddenRequest(httpContext);
                return false;
            }


            if (string.IsNullOrWhiteSpace(contactId))
            {
                ForbiddenRequest(httpContext);
                return false;
            }

            var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
            var permissions = await userPermissionService!.GetContactAuthorizationAsync(int.Parse(contactId!), accountId);

            if (permissions == null || !permissions.Any(x => requiredClaims.Contains(x)))
            {
                ForbiddenRequest(httpContext);
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> CheckRoles(int? accountId, string? contactId, HttpContext httpContext)
    {
        if (accountId.HasValue && !string.IsNullOrWhiteSpace(contactId))
        {
            var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
            var permissions = await userPermissionService!.GetContactAuthorizationAsync(int.Parse(contactId!), GlobalsConstants.CollaboratorAccountId);
            if (permissions == null || !permissions.Any(x => GlobalsConstants.NoAccountCheckPermissions.Contains(x)))
            {
                var accountService = httpContext.RequestServices.GetRequiredService<IAccountService>();
                var relatedAccounts = await accountService!.GetContactRolesAsync(int.Parse(contactId!));
                if (!relatedAccounts.Items.Any(a => a.AccountId == accountId.Value))
                {
                    ForbiddenAccount(httpContext, accountId.Value);
                    return false;
                }
            }
        }

        return true;
    }

    private static int? ValidateAccountId(HttpContext httpContext)
    {
        // account number (ged url), we return null
        if (httpContext.Request.Path.ToString().Contains("ged-services"))
        {
            return null;
        }

        var accountIdParam = (string)httpContext.Request.Query["accountId"];
        if (string.IsNullOrWhiteSpace(accountIdParam))
        {
            const string pattern = @"/accounts/(\d+)";
            Match match = Regex.Match(httpContext.Request.Path, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (match.Success)
            {
                accountIdParam = (string)match.Groups[1].Value;
            }
        }

        if (int.TryParse(accountIdParam, out var accountId))
        {
            return accountId;

        }
        return null;
    }

    private static List<string> ValidateRequireClaim(HttpContext httpContext)
    {
        httpContext
            .Items.DownstreamRoute()
            .RouteClaimsRequirement
            .TryGetValue(httpContext.Request.Method, out var claims);

        if (string.IsNullOrWhiteSpace(claims))
        {
            return new List<string>();
        }

        return claims.Split(',').Select(x => x.Trim()).ToList();
    }

    private static string ValidateUserIdentity(HttpContext httpContext)
    {
        var token = JwtHelper.ExtractBearerToken(httpContext.Request);


        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var userEmail = JwtHelper.ExtractUserEmailFromToken(token);

        return string.IsNullOrWhiteSpace(userEmail) ? string.Empty : userEmail;
    }

    private static void ForbiddenRequest(HttpContext httpContext)
    {
        var downstreamRoute = httpContext.Items.DownstreamRoute();
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Items.SetError(new UnauthorizedError(
                           $"{httpContext!.User!.Identity!.Name} unable to access {downstreamRoute.UpstreamPathTemplate.OriginalValue}"));
    }

    private static void ForbiddenAccount(HttpContext httpContext, int accountId)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Items.SetError(new UnauthorizedError(
                           $"{httpContext!.User!.Identity!.Name} unable to access {accountId}"));
    }
}