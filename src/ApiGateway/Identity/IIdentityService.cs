namespace ApiGateway.Identity
{
    public interface IIdentityService
    {
        /// <summary>
        /// Validates if the user belongs to the Collaborators Azure AD security group.
        /// </summary>
        /// <param name="httpContext">The HTTP context containing the user claims.</param>
        /// <returns>True if the user is in the Collaborators security group, false otherwise.</returns>
        bool ValidateCollaborator(HttpContext httpContext);

        /// <summary>
        /// Validates if the user belongs to the Administrators Azure AD security group.
        /// </summary>
        /// <param name="httpContext">The HTTP context containing the user claims.</param>
        /// <returns>True if the user is in the Administrators security group, false otherwise.</returns>
        bool ValidateAdministrator(HttpContext httpContext);

        /// <summary>
        /// Validates if the customer exists in Gigya.
        /// </summary>
        /// <param name="email">The customer's email address.</param>
        /// <returns>True if the customer exists in Gigya, false otherwise.</returns>
        Task<bool> ValidateCustomerAsync(string email);
    }
}
