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
                throw new ArgumentNullException(nameof(principal));
            }

            var emailClaim = principal.FindFirst(ClaimTypes.Email);

            if (emailClaim == null)
            {
                throw new ArgumentException($"The principal does not contains an email claim ({ClaimTypes.Email})", nameof(principal));
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
                throw new ArgumentNullException(nameof(principal));
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
                throw new ArgumentNullException(nameof(principal));
            }

            return principal.IsInRole(CustomerRole);
        }
    }
}
