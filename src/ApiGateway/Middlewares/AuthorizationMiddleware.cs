using ApiGateway.Authorization;
using Ocelot.Middleware;
using ApiGateway.Contact;
using ApiGateway.Helpers;
using System.Text.RegularExpressions;
using ApiGateway.Account;
using ApiGateway.Constants;
using ApiGateway.Exceptions;
using ApiGateway.Identity;
using ApiGateway.Identity.Extensions;
using System.Text;
using ApiGateway.Extensions;
using ApiGateway.Cache;

namespace ApiGateway.Middlewares;

public static class AuthorizationMiddleware
{
    public static Func<HttpContext, Func<Task>, Task> AuthorizationFilter => async (httpContext, next) =>
    {
        if (IsAnonymousRoute(httpContext))
        {
            await next.Invoke();
            return;
        }
        var cacheService = httpContext.RequestServices.GetService<ICacheService>();
        var userEmail = ValidateUserIdentity(httpContext);
        var contactService = httpContext.RequestServices.GetRequiredService<IContactService>();
        var contactId = await contactService!.GetContactIdAsync(userEmail);
        var requiredClaims = ValidateRequireClaim(httpContext);
        int? accountId = ValidateAccountId(httpContext);
        var identityService = httpContext.RequestServices.GetRequiredService<IIdentityService>();
        var isValidIdentity = await httpContext.IdentityServiceValidations(userEmail, identityService);

        string content = await PeekBody(httpContext.Request);

        cacheService!.GetOrCreate(GlobalsConstants.cacheContactId, contactId);
        cacheService.GetOrCreate(GlobalsConstants.cacheAccountId, accountId.ToString());
        cacheService.GetOrCreate(GlobalsConstants.cacheContent, content);

        if (!isValidIdentity)
        {
            return;
        }

        if (requiredClaims.Count == 0 && !accountId.HasValue)
        {
            await next.Invoke();
            return;
        }

        if (!await CheckClaims(requiredClaims, userEmail, accountId, contactId, httpContext))
        {
            return;
        }

        if (await AuthorizationHelper.SkipRoleCheck(accountId, int.Parse(contactId!), httpContext))
        {
            await next.Invoke();
            return;
        }

        var hasRoleOnAccount = await AuthorizationHelper.CheckRoles(accountId, null, int.Parse(contactId!), httpContext);
        if (!hasRoleOnAccount)
        {
            throw new GatewayException(StatusCodes.Status403Forbidden, Errors.RoleRequiredCode, string.Format(Errors.RoleRequiredMessage, contactId, accountId));
        }

        await next.Invoke();
    };

    private static bool IsAnonymousRoute(HttpContext httpContext)
    {
        var downstreamRoutes = httpContext.Items.DownstreamRoute();
        return !downstreamRoutes.IsAuthenticated;
    }

    public static async Task<string> PeekBody(HttpRequest request)
    {
        try
        {
            request.EnableBuffering();
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }
        finally
        {
            request.Body.Position = 0;
        }
    }


    private static async Task<bool> CheckClaims(List<string> requiredClaims, string userEmail, int? accountId, string? contactId, HttpContext httpContext)
    {
        if (requiredClaims.Count != 0)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(userEmail)));
            }
            if (string.IsNullOrWhiteSpace(contactId))
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(contactId)));
            }

            var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var parsedContactId = int.Parse(contactId!);
            var permissions = await userPermissionService.GetAllContactAuthorizationAsync(parsedContactId, accountId) ?? [];

            if (!permissions.Any(x => requiredClaims.Contains(x)))
            {
                string permissionCodes = permissions == null ? "" : String.Join(",", permissions);
                string requiredClaimsCodes = String.Join(",", requiredClaims);
                throw new GatewayException(StatusCodes.Status403Forbidden, Errors.PermissionRequiredCode, Errors.PermissionRequiredMessage);
            }
        }

        return true;
    }

    private static int? ValidateAccountId(HttpContext httpContext)
    {
        if (Array.Exists(GlobalsConstants.NoAccountCheckEndpoints, e => httpContext.Request.Path.ToString().Contains(e)))
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

        if (string.IsNullOrWhiteSpace(accountIdParam))
        {
            var prospectAccountRoutePrefix = AuthorizationHelper.GetProspectAccountRoutePrefix(httpContext.Request.Path.Value ?? string.Empty);
            if (prospectAccountRoutePrefix is not null)
            {
                accountIdParam = AuthorizationHelper.ExtractProspectAccountIdFromRoute(httpContext.Request.Path.Value!, prospectAccountRoutePrefix)?.ToString();
            }
        }

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

    internal static List<string> ValidateRequireClaim(HttpContext httpContext)
    {
        var down = httpContext
            .Items.DownstreamRoute();

        if (down == null)
        {
            return new List<string>();
        }

        down
            .RouteClaimsRequirement
            .TryGetValue(httpContext.Request.Method, out var claims);

        if (string.IsNullOrWhiteSpace(claims))
        {
            return new List<string>();
        }

        return claims.Split(',').Select(x => x.Trim()).ToList();
    }

    internal static string ValidateUserIdentity(HttpContext httpContext)
    {
        var token = JwtHelper.ExtractBearerToken(httpContext.Request);


        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var userEmail = JwtHelper.ExtractUserEmailFromToken(token);

        return string.IsNullOrWhiteSpace(userEmail) ? string.Empty : userEmail;
    }




}
