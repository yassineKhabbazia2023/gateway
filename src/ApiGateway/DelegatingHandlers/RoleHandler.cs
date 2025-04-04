using ApiGateway.Helpers;
using System.Web;
using System.Net;
using ApiGateway.Exceptions;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace ApiGateway.DelegatingHandlers
{
    /// <summary>
    /// RoleHandler is a custom DelegatingHandler responsible for enforcing role-based access control.
    /// It checks the user's roles to ensure they have appropriate permissions to access a specific route.
    /// </summary>
    public class RoleHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<RoleHandler> _logger;

        public RoleHandler(ILogger<RoleHandler> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// This method handles the role validation for each incoming request.
        /// It checks the role requirements based on the user's ID, the account ID, and the route's requirements.
        /// It returns a 403 Forbidden if the user does not have the required permissions.
        /// </summary>
        /// <param name="request">The incoming HTTP request.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, which contains the HTTP response.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                // Extract the current user id from the 'CurrentUser' header
                if (!request.Headers.TryGetValues("CurrentUser", out var contactIdFromHeader))
                {
                    _logger.LogWarning("[Response]: 403 - [Handler]: RoleHandler - [Reason]: 'CurrentUser' header is missing.");
                    return await ReturnError(new GatewayException(403, Errors.NotFoundContactCode, string.Format(Errors.NotFoundContactMessage, "CurrentUser")));
                }

                if (!int.TryParse(contactIdFromHeader.FirstOrDefault(), out int currentUserId))
                {
                    _logger.LogWarning("[Response]: 403 - [Handler]: RoleHandler - [Reason]: Invalid 'CurrentUser' contactId.");
                    return await ReturnError(new GatewayException(403, Errors.NotFoundContactCode, string.Format(Errors.NotFoundContactMessage, "CurrentUser")));
                }

                // Extract accountId and contactId from the query string
                var queryParameters = HttpUtility.ParseQueryString(request.RequestUri?.Query!);
                int? accountId = AuthorizationHelper.ParseQueryParameter<int>("accountId", queryParameters);
                int? contactId = AuthorizationHelper.ParseQueryParameter<int>("contactId", queryParameters);
                string? accountNumber = queryParameters["accountNumber"];

                // Extract contactId from route param if exists
                // Si contactId est toujours null, tente d'extraire un entier de l'URL
                // exp: /gtw/customer-wallet/api/contacts/188/accounts/602 (ContactId = 188)
                if (contactId == null)
                {
                    var path = request.RequestUri?.AbsolutePath!;

                    // Utilisation de Regex pour extraire le premier entier trouvé
                    var match = Regex.Match(path, @"\d+");

                    // Si un entier est trouvé et c'est un match valide
                    if (match.Success && int.TryParse(match.Value, out int parsedId))
                    {
                        contactId = parsedId;
                    }
                }

                // Case 1: If the logged-in user is accessing their own info, skip the role check.
                if (contactId == currentUserId)
                    return await base.SendAsync(request, cancellationToken);

                // Case 2: If the logged-in user is a super admin, skip the role check.
                if (await AuthorizationHelper.SkipRoleCheck(accountId, currentUserId, _httpContextAccessor.HttpContext!))
                    return await base.SendAsync(request, cancellationToken);

                // Case 3: If accountId exists, check if the user has the role for the specified account.
                if (accountId.HasValue)
                {
                    var hasRole = await AuthorizationHelper.CheckRoles(accountId, null, currentUserId, _httpContextAccessor.HttpContext!);
                    if (!hasRole)
                    {
                        _logger.LogWarning($"[Response]: 403 - [Handler]: RoleHandler - [Function]: CheckRoles - [Reason]: No role for contactId: {currentUserId} on accountId: {accountId}");
                        return await ReturnError(new GatewayException(403, Errors.NoRoleOnAccountCode, string.Format(Errors.NoRoleOnAccountMessage, currentUserId, accountId)));
                    }
                }
                // Case 4: If accountNumber exists, check if the user has the role for the specified account.
                else if (!string.IsNullOrEmpty(accountNumber))
                {
                    var hasRole = await AuthorizationHelper.CheckRoles(null, accountNumber, currentUserId, _httpContextAccessor.HttpContext!);
                    if (!hasRole)
                    {
                        _logger.LogWarning($"[Response]: 403 - [Handler]: RoleHandler - [Function]: CheckRoles - [Reason]: No role for contactId: {currentUserId} on accountNumber: {accountNumber}");
                        return await ReturnError(new GatewayException(403, Errors.NoRoleOnAccountCode, string.Format(Errors.NoRoleOnAccountMessage, currentUserId, accountNumber)));
                    }
                }
                else
                {
                    // Case 5: If accountId does not exist, check if the user has a common role on a shared account with the upstream user.
                    var hasCommonAccountRole = await AuthorizationHelper.CheckContactsCommonAccountRole(currentUserId, contactId, _httpContextAccessor.HttpContext!);
                    if (!hasCommonAccountRole)
                    {
                        _logger.LogWarning($"[Response]: 403 - [Handler]: RoleHandler - [Function]: CheckContactsCommonAccountRole - [Reason]: No common account role for contactId: {contactId} and currentUserId: {currentUserId}");
                        return await ReturnError(new GatewayException(403, Errors.NoCommonAccountRoleCode, string.Format(Errors.NoCommonAccountRoleMessage, currentUserId, contactId)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Handler]: RoleHandler - [Error]: {ex.Message}");
                return await ReturnError(new GatewayException(403, Errors.UnexpectedExceptionCode, string.Format(Errors.UnexptectedExceptionMessage)));
            }

            // If all checks pass, proceed with the request.
            return await base.SendAsync(request, cancellationToken);
        }


        /// <summary>
        /// Creates a standardized error response in the form of an HTTP response message.
        /// </summary>
        /// <param name="gatewayException">The exception containing the error details.</param>
        /// <returns>A task that represents the asynchronous creation of the error response.</returns>
        private Task<HttpResponseMessage> ReturnError(GatewayException gatewayException)
        {
            // Ensure the correct ErrorMessage and ErrorCode are being returned
            var errorResponse = new HttpResponseMessage((HttpStatusCode)gatewayException.StatusCode)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { ErrorMessage = gatewayException.Message, ErrorCode = gatewayException.Code }))
            };

            return Task.FromResult(errorResponse);
        }

    }
}
