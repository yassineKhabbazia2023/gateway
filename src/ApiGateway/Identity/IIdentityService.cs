namespace ApiGateway.Identity
{
    public interface IIdentityService
    {
        bool ValidateCollaborator(HttpContext httpContext);

        Task<bool> ValidateCustomerAsync(string email);
    }
}
