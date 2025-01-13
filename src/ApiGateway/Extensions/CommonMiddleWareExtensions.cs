using ApiGateway.Identity.Extensions;
using ApiGateway.Identity;
using Ocelot.Authorization;
using Ocelot.Middleware;
using Newtonsoft.Json;
using System.Text;

namespace ApiGateway.Extensions
{
    public static class CommonMiddleWareExtensions
    {

        public static void ForbiddenRequest(this HttpContext httpContext)
        {
            var downstreamRoute = httpContext.Items.DownstreamRoute();
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            httpContext.Items.SetError(new UnauthorizedError(
                               $"{httpContext!.User!.Identity!.Name} unable to access {downstreamRoute.UpstreamPathTemplate.OriginalValue}"));
        }

        public static void ForbiddenAccount(this HttpContext httpContext, int accountId)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            httpContext.Items.SetError(new UnauthorizedError(
                               $"{httpContext!.User!.Identity!.Name} unable to access {accountId}"));
        }
        public static async Task<bool> IdentityServiceValidations(this HttpContext httpContext, string userEmail, IIdentityService identityServiceProvider)
        {
            bool isCollaborator = httpContext.User.IsCollaborator();
            bool isCustomer = httpContext.User.IsCustomer();
            var logger = httpContext.RequestServices.GetService<ILogger<Program>>();

            if (isCollaborator)
            {
                var isValidCollaborator = identityServiceProvider.ValidateCollaborator(httpContext);
                if (!isValidCollaborator)
                {
                    logger.LogWarning("[Response]:403 - [Function]:IdentityServiceValidations - [Reason]: Not Valid Collaborator");
                    ForbiddenRequest(httpContext);
                    return false;
                }
            }

            if (isCustomer)
            {
                var isValidCustomer = await identityServiceProvider.ValidateCustomerAsync(userEmail);
                if (!isValidCustomer)
                {
                    logger.LogWarning("[Response]:403 - [Function]:IdentityServiceValidations - [Reason]: Not Valid Customer");
                    ForbiddenRequest(httpContext);
                    return false;
                }
            }

            return true;
        }

        

    }
}
