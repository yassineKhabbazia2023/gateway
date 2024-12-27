using ApiGateway.Account;
using ApiGateway.Cache;
using ApiGateway.Constants;
using ApiGateway.Extensions;
using ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using System.Net.Http;

namespace ApiGateway.Middlewares
{
    public class CustomerCreationMiddleWare
    {

        /// <summary>
        /// Here we check the body of the request that contains accountNumber is matching the accountId sent in the url because we 
        /// want to block the user from adding a customer for any account number he wants to
        /// this implementation would be better if it is in contact api. 
        /// It is not allowed to make api calls from contact to account to check the roles. 
        /// that is why I have implemented it here,
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static async Task InvokeAsync(HttpContext context, Func<Task> next)
        {
            if (context.Request.GetDisplayUrl().Contains("/gtw/account/api/customers?accountId=") && context.Request.Method == HttpMethod.Post.Method)
            {
                var cacheService = context.RequestServices.GetRequiredService<ICacheService>();
                string? contactId = cacheService.GetOrCreate<string>(GlobalsConstants.cacheContactId);
                string? accountId = cacheService.GetOrCreate<string>(GlobalsConstants.cacheAccountId);
                string? content = cacheService.GetOrCreate<string>(GlobalsConstants.cacheContent);

                var contactCreated = JsonConvert.DeserializeObject<CreatedContact>(content);
                int.TryParse(contactId, out int customerId);
                int.TryParse(accountId, out int entityId);

                if (customerId == default || entityId == default || contactCreated == null || string.IsNullOrEmpty(contactCreated?.AccountNumber))
                {
                    context.ForbiddenRequest();
                    return;
                }

                var accountService = context.RequestServices.GetRequiredService<IAccountService>();
                var relatedAccounts = await accountService!.GetContactRolesAsync(customerId);
                var hasTheRightToCreateCustomer = relatedAccounts.Items.Any(x => x.AccountNumber == contactCreated?.AccountNumber);
                if (!hasTheRightToCreateCustomer)
                {
                    context.ForbiddenRequest();
                    return;
                }
            }
            await next.Invoke();
        }

    }
}
