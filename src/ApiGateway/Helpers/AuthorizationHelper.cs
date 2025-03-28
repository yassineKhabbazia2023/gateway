using System.Collections.Specialized;
using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Constants;

namespace ApiGateway.Helpers
{
    public static class AuthorizationHelper
    {
        public static async Task<bool> SkipRoleCheck(int? accountId, int? contactId, HttpContext httpContext)
        {
            if (accountId.HasValue && contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
                var permissions = await userPermissionService!.GetContactAuthorizationAsync((int)contactId, accountId);

                if (permissions != null && permissions.Any(x => GlobalsConstants.NoRoleCheckPermissions.Contains(x)))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> CheckRoles(int? accountId, int? contactId, HttpContext httpContext)
        {
            if (accountId.HasValue && contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
                var permissions = await userPermissionService!.GetContactAuthorizationAsync((int)contactId, GlobalsConstants.CollaboratorAccountId);
                if (permissions == null || !permissions.Any(x => GlobalsConstants.NoAccountCheckPermissions.Contains(x)))
                {
                    var accountService = httpContext.RequestServices.GetRequiredService<IAccountService>();
                    var hasRoleOnAccount = await accountService!.CheckContactRoleAsync((int)contactId, accountId, null);

                    return hasRoleOnAccount;
                }
            }

            return true;
        }

        public static async Task<bool> CheckContactsCommonAccountRole(int currentUserId, int? contactId, HttpContext httpContext)
        {
            if (contactId.HasValue)
            {
                var userPermissionService = httpContext.RequestServices.GetRequiredService<IAuthorizationSevice>();
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
