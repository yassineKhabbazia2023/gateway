using ApiGateway.Identity.Extensions;
using ApiGateway.Identity;
using Ocelot.Authorization;
using Ocelot.Middleware;
using Newtonsoft.Json;
using System.Text;
using Ocelot.Responder;
using ApiGateway.Exceptions;

namespace ApiGateway.Extensions
{
    public static class CommonMiddleWareExtensions
    {

        public static void ForbiddenRequest(this HttpContext httpContext)
        {
            var downstreamRoute = httpContext.Items.DownstreamRoute();
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            httpContext.Items.SetError(new UnauthorizedError(
                               $"{httpContext!.User!.Identity!.Name} impossible d'accéder {downstreamRoute.UpstreamPathTemplate.OriginalValue}"));
        }

        public static void ForbiddenAccount(this HttpContext httpContext, int accountId)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            httpContext.Items.SetError(new UnauthorizedError(
                               $"{httpContext!.User!.Identity!.Name} impossible d'accéder {accountId}"));
        }
        public static async Task<bool> IdentityServiceValidations(this HttpContext httpContext, string userEmail, IIdentityService identityServiceProvider)
        {
            bool isCollaborator = httpContext.User.IsCollaborator();
            bool isCustomer = httpContext.User.IsCustomer();
            if (isCollaborator)
            {
                var isValidCollaborator = identityServiceProvider.ValidateCollaborator(httpContext);
                if (!isValidCollaborator)
                {
                    throw new GatewayException((int)StatusCodes.Status403Forbidden, Errors.NotValidCollaboratorCode, string.Format(Errors.NotValidCollaboratorMessage, userEmail));
                }
            }

            if (isCustomer)
            {
                var isValidCustomer = await identityServiceProvider.ValidateCustomerAsync(userEmail);
                if (!isValidCustomer)
                {
                    throw new GatewayException(StatusCodes.Status403Forbidden, Errors.NotValidCustomerCode, string.Format(Errors.NotValidCustomerMessage, userEmail));
                }
            }

            return true;
        }



    }
}
