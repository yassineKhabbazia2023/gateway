using ApiGateway.Account;
using ApiGateway.Middlewares;
using ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Configuration;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ApiGateway.UnitTests.MiddleWares
{
    public class CustomerInvitationMiddleWareTests
    {
        [Fact]
        public async Task InvokeAsync_Should_Return403_If_contactId_IsNotFound()
        {
            string path = "/gtw/account/api/customers/invite";
            string method = HttpMethod.Post.Method;
            var context = CreateHttpContext(contactId:null);

            await CustomerInvitationMiddleWare.InvokeAsync(context, () => Task.CompletedTask);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }


        [Fact]
        public async Task InvokeAsync_Should_Return403_If_accountNumber_IsNotFound()
        {
            string path = "/gtw/account/api/customers/invite";
            string method = HttpMethod.Post.Method;
            var context = CreateHttpContext(1234, accountNumber: null);

            await CustomerInvitationMiddleWare.InvokeAsync(context, () => Task.CompletedTask);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_Should_ReturnOK_If_accountNumber_ExistedInRoles()
        {
            string path = "/gtw/account/api/customers/invite";
            string method = HttpMethod.Post.Method;
            Paging<Models.Account> paging = new Fixture().Create<Paging<Models.Account>>();
            paging.Items.First().AccountNumber = "12345123";
            var context = CreateHttpContext(1234, accountNumber: "12345123");

            // Setup service provider
            var mockAccountService = new Mock<IAccountService>();
            var services = new ServiceCollection();
            services.AddSingleton(mockAccountService.Object);
            context.RequestServices = services.BuildServiceProvider();
           
            mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(paging);

            await CustomerInvitationMiddleWare.InvokeAsync(context, () => Task.CompletedTask);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
            mockAccountService.Verify(x => x.GetContactRolesAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_Should_ReturnForbidden_If_accountNumber_DoesNotExistedInRoles()
        {
            string path = "/gtw/account/api/customers/invite";
            string method = HttpMethod.Post.Method;
            Paging<Models.Account> paging = new Fixture().Create<Paging<Models.Account>>();
            var context = CreateHttpContext(1234, accountNumber: "THIS_ACCOUNT_DOES_NOT_EXIST");

            // Setup service provider
            var mockAccountService = new Mock<IAccountService>();
            var services = new ServiceCollection();
            services.AddSingleton(mockAccountService.Object);
            context.RequestServices = services.BuildServiceProvider();

            mockAccountService.Setup(x => x.GetContactRolesAsync(It.IsAny<int>())).ReturnsAsync(paging);

            await CustomerInvitationMiddleWare.InvokeAsync(context, () => Task.CompletedTask);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
            mockAccountService.Verify(x => x.GetContactRolesAsync(It.IsAny<int>()), Times.Once);
        }





        private HttpContext CreateHttpContext(
            int? contactId = null,
       string path = "/gtw/account/api/customers/invite",
       string method = "POST",
       string accountNumber = "456789")
        {
            if (contactId != null)
            {
                path += $"/{contactId}";
            }
           
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            if (!string.IsNullOrEmpty(accountNumber))
            {
                context.Request.QueryString = new QueryString($"?accountNumber={accountNumber}");
            }
            context.Items["DownstreamRoute"] = Dummies.GenerateDownStream(new Dictionary<string, string>());

            return context;
        }
    }
}
