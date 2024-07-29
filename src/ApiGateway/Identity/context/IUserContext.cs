using System.Security.Claims;

namespace ApiGateway.Identity.context
{
    public interface IUserContext
    {
        /// <summary>
        /// Gets the security information of the current authenticated user.
        /// </summary>
        ClaimsPrincipal User { get; }
    }
}
