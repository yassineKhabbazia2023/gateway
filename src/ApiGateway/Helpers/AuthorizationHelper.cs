using System.Collections.Specialized;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Constants;

namespace ApiGateway.Helpers
{
    public static class AuthorizationHelper
    {
        // Upstream route prefixes where the first path parameter represents an accountId.
        public static readonly string[] ProspectAccountRoutePrefixes =
        [
            "/gtw/prospect/api/onboarding/"
        ];

        /// <summary>
        /// Determines whether the request path targets a Prospect route whose path identifier must be handled as an account identifier.
        /// </summary>
        /// <param name="path">The request path to evaluate.</param>
        /// <returns>The matched Prospect route prefix, or null when the path is not a Prospect account route.</returns>
        public static string? GetProspectAccountRoutePrefix(string path)
        {
            return ProspectAccountRoutePrefixes.FirstOrDefault(prefix =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Extracts the account identifier from supported Prospect upstream routes.
        /// </summary>
        /// <param name="path">The incoming Prospect upstream path.</param>
        /// <param name="prefix">The matched Prospect route prefix.</param>
        /// <returns>The account identifier found in the Prospect route, or null if none is found.</returns>
        public static int? ExtractProspectAccountIdFromRoute(string path, string prefix)
        {
            var remainingPath = path[prefix.Length..];
            var accountIdSegment = remainingPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (int.TryParse(accountIdSegment, out var parsedId))
            {
                return parsedId;
            }

            return null;
        }

        public static async Task<bool> SkipRoleCheck(int? accountId, int? contactId, HttpContext httpContext)
        {
            if (accountId.HasValue && contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
                var permissions = await userPermissionService!.GetContactAuthorizationAsync((int)contactId, accountId);

                if (permissions != null && permissions.Any(x => GlobalsConstants.NoRoleCheckPermissions.Contains(x)))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> CheckRoles(int? accountId, string? accountNumber, int? contactId, HttpContext httpContext)
        {
            bool isAccountValid = accountId.HasValue || !string.IsNullOrEmpty(accountNumber);

            if (isAccountValid && contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
                var permissions = await userPermissionService!.GetContactAuthorizationAsync((int)contactId, GlobalsConstants.CollaboratorAccountId);
                if (permissions == null || !permissions.Any(x => GlobalsConstants.NoAccountCheckPermissions.Contains(x)))
                {
                    var accountService = httpContext.RequestServices.GetRequiredService<IAccountService>();
                    var hasRoleOnAccount = await accountService!.CheckContactRoleAsync((int)contactId, accountId, accountNumber);

                    return hasRoleOnAccount;
                }
            }

            return true;
        }

        public static async Task<bool> CheckContactsCommonAccountRole(int currentUserId, int? contactId, HttpContext httpContext)
        {
            if (contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
                var permissions = await userPermissionService!.GetContactAuthorizationAsync(currentUserId, GlobalsConstants.CollaboratorAccountId);
                if (permissions == null || !permissions.Any(x => GlobalsConstants.NoAccountCheckPermissions.Contains(x)))
                {
                    var accountService = httpContext.RequestServices.GetRequiredService<IAccountService>();
                    var hasCommonAccountRole = await accountService!.CheckContactsCommonAccountRole(currentUserId, (int)contactId);

                    return hasCommonAccountRole;
                }
            }

            return true;
        }

        /// <summary>
        /// Utility method to parse query string parameters into a specified type.
        /// </summary>
        /// <typeparam name="T">The target type for the parsed parameter.</typeparam>
        /// <param name="parameterName">The name of the query parameter.</param>
        /// <param name="queryParameters">The collection of query parameters.</param>
        /// <returns>The parsed value of the parameter, or null if it couldn't be parsed.</returns>
        // Method for Nullable Types (e.g., int?, double?)
        public static T? ParseQueryParameter<T>(string parameterName, NameValueCollection queryParameters) where T : struct
        {
            var value = queryParameters[parameterName];

            // If the value is null or empty, return null for nullable types
            if (string.IsNullOrEmpty(value))
            {
                return null;  // Return null for nullable types
            }

            try
            {
                // Convert to T? for nullable types
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return null;  // Return null if conversion fails
            }
        }
    }
}
