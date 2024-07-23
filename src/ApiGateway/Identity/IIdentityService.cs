namespace ApiGateway.Identity
{
    public interface IIdentityService
    {
        bool IsCollaborator(HttpContext httpContext);

        bool IsCustomer(HttpContext httpContext);

        bool ValidateCollaborator(HttpContext httpContext);

        Task<bool> ValidateCustomerAsync(string email);
    }
}
