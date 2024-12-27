using ApiGateway.Account;
using ApiGateway.Extensions;
using Microsoft.AspNetCore.Http.Extensions;

namespace ApiGateway.Middlewares
{
    public class CustomerInvitationMiddleWare
    {
        /// <summary>
        /// Here the user does not have a valid token because 
        /// So we want to check if the invitation is made for the correct contact and accountNumber 
        /// We can not be based on the token but we can check if this contact has a role inside this account.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static async Task InvokeAsync(HttpContext context, Func<Task> next)
        {

            if (context.Request.GetDisplayUrl().Contains("/gtw/account/api/customers/invite") && context.Request.Method == HttpMethod.Post.Method)
            {
                string contactId = context.Request.Path.Value.Split('/').Last();
                bool successContactParsing = int.TryParse(contactId, out int currentContactId);
                context.Request.Query.TryGetValue("accountNumber",out var accountNumber);

                if (currentContactId == default || string.IsNullOrEmpty(accountNumber))
                {
                    context.ForbiddenRequest();
                    return;
                }

                var accountService = context.RequestServices.GetService<IAccountService>();
                var roles = await accountService.GetContactRolesAsync(currentContactId);
                bool hasTheRightToInvite = roles.Items.Any(x => x != null && x.AccountNumber.ToLower().Trim().Equals(accountNumber.ToString().ToLower().Trim()));
                if (!hasTheRightToInvite)
                {
                    context.ForbiddenRequest();
                    return;
                }
            }

            await next.Invoke();
        }

    }
}
