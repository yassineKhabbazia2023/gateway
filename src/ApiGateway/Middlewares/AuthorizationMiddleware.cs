using ApiGateway.Authorization;
using Ocelot.Middleware;
using ApiGateway.Contact;
using ApiGateway.Helpers;
using Ocelot.Authorization;
using System.Text.RegularExpressions;

namespace ApiGateway.Middlewares;

public static class AuthorizationMiddleware
{
    public static Func<HttpContext, Func<Task>, Task> AuthorizationFilter => async (httpContext, next) =>
    {
        var requiredClaims = ValidateRequireClaim(httpContext);
        if (requiredClaims.Count == 0)
        {
            await next.Invoke();
            return;
        }

        var userEmail = ValidateUserIdentity(httpContext);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            ForbiddenRequest(httpContext);
            return;
        }

        var contactService = httpContext.RequestServices.GetRequiredService<IContactService>();
        var contactId = await contactService!.GetContactIdAsync(userEmail);
        if (string.IsNullOrWhiteSpace(contactId))
        {
            ForbiddenRequest(httpContext);
            return;
        }

        int? accountId = ValidateAccountId(httpContext);
        var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
        var permissions = await userPermissionService!.GetContactAuthorizationAsync(int.Parse(contactId!), accountId);

        if (permissions == null || !permissions.Any(x => requiredClaims.Contains(x)))
        {
            ForbiddenRequest(httpContext);
            return;
        }

        await next.Invoke();
    };

    private static int? ValidateAccountId(HttpContext httpContext)
    {
        var accountIdParam = httpContext.Request.Query["accountId"];
        if (string.IsNullOrWhiteSpace(accountIdParam))
        {
            const string pattern = @"/accounts/(\d+)";
            Match match = Regex.Match(httpContext.Request.Path, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (match.Success)
            {
                accountIdParam = match.Groups[1].Value;
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

        return claims.Split(',').ToList();
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
}