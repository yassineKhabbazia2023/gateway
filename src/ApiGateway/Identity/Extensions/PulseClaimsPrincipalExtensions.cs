using ApiGateway.Exceptions;
using System.Security.Claims;

namespace ApiGateway.Identity.Extensions
{
    /// <summary>
    /// Contains extensions methods on the <see cref="ClaimsPrincipal"/> class to retrieve <c>Constellation</c> user information.
    /// </summary>
    public static class PulseClaimsPrincipalExtensions
    {
        private static readonly string CollaboratorRole = "Collaborator";

        private static readonly string CustomerRole = "Customer";

        private static readonly string AdministratorRole = "Administrator";

        /// <summary>
        /// Gets the login name of the user represented by his <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal"><see cref="ClaimsPrincipal"/> which contains the claims of the authenticated user.</param>
        /// <returns>The login name of the user represented by his <paramref name="principal"/>.</returns>
        /// <exception cref="ArgumentNullException">If the specified <paramref name="principal"/> argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">If the specified <paramref name="principal"/> does not contains the <see cref="ClaimTypes.Email"/>
        /// claim.</exception>
        public static string GetEmail(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(principal)));
            }

            var emailClaim = principal.FindFirst(ClaimTypes.Email);

            if (emailClaim == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(emailClaim)));
            }

            return emailClaim.Value;
        }

        /// <summary>
        /// Determines if the specified <paramref name="principal"/> has the <see cref="CollaboratorRole"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to determine if the user has the
        /// <see cref="CollaboratorRole"/>.</param>
        /// <returns><see langword="true"/> if the <paramref name="principal"/> has the <see cref="CollaboratorRole"/>
        /// <see langword="false"/> in otherwise.</returns>
        /// <exception cref="ArgumentNullException">If the specified <paramref name="principal"/> argument is <see langword="null"/>.</exception>
        public static bool IsCollaborator(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {

                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(principal)));
            }

            return principal.IsInRole(CollaboratorRole);
        }

        /// <summary>
        /// Determines if the specified <paramref name="principal"/> has the <see cref="CustomerRole"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to determine if the user has the
        /// <see cref="CustomerRole"/>.</param>
        /// <returns><see langword="true"/> if the <paramref name="principal"/> has the <see cref="CustomerRole"/>
        /// <see langword="false"/> in otherwise.</returns>
        /// <exception cref="ArgumentNullException">If the specified <paramref name="principal"/> argument is <see langword="null"/>.</exception>
        public static bool IsCustomer(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(principal)));
            }

            return principal.IsInRole(CustomerRole);
        }

        /// <summary>
        /// Determines if the specified <paramref name="principal"/> has the <see cref="AdministratorRole"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to determine if the user has the
        /// <see cref="AdministratorRole"/>.</param>
        /// <returns><see langword="true"/> if the <paramref name="principal"/> has the <see cref="AdministratorRole"/>
        /// <see langword="false"/> in otherwise.</returns>
        /// <exception cref="ArgumentNullException">If the specified <paramref name="principal"/> argument is <see langword="null"/>.</exception>
        public static bool IsAdministrator(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(principal)));
            }

            return principal.IsInRole(AdministratorRole);
        }
    }
}
