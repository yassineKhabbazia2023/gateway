using ApiGateway.Account;
using ApiGateway.Cache;
using ApiGateway.Constants;
using ApiGateway.Contact;
using ApiGateway.Middlewares;
using ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Net;
using System.Runtime.CompilerServices;

namespace ApiGateway.UnitTests.MiddleWares
{
    public class CustomerCreationMiddlewareTests
    {
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<IAccountService> _mockAccountService;
        public CustomerCreationMiddlewareTests()
        {
            _mockCacheService = new Mock<ICacheService>();
            _mockAccountService = new Mock<IAccountService>();
        }

        [Fact]
        public async Task InvokeAsync_Should_ReturnForbidden_IfContactId_NotValid()
        {

            string htmlPath = "/gtw/account/api/customers?accountId=306";
            string method = HttpMethod.Post.Method;
            var contactCreated = new CreatedContact { AccountNumber = "123456" };
            var httpContext = Dummies.DummyHttpContext(htmlPath, method, string.Empty, null);
            httpContext.Request.Path = htmlPath;
            httpContext.Request.Method = method;
            httpContext.RequestServices = new ServiceCollection()
                       .AddSingleton(_mockAccountService.Object)
                       .AddSingleton(_mockCacheService.Object)
                       .BuildServiceProvider();

            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null)).Returns(string.Empty)
                .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null)).Returns("123456")
               .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null)).Returns(JsonConvert.SerializeObject(contactCreated))
                .Verifiable();

            Func<Task> task = () => Task.CompletedTask;

            await CustomerCreationMiddleWare.InvokeAsync(httpContext, task);

            httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_Should_ReturnForbidden_IfAccountId_NotValid()
        {

            string htmlPath = "/gtw/account/api/customers?accountId=306";
            string method = HttpMethod.Post.Method;
            var contactCreated = new CreatedContact { AccountNumber = "123456" };
            var httpContext = Dummies.DummyHttpContext(htmlPath, method, string.Empty, null);
            httpContext.Request.Path = htmlPath;
            httpContext.Request.Method = method;
            httpContext.RequestServices = new ServiceCollection()
                       .AddSingleton(_mockAccountService.Object)
                       .AddSingleton(_mockCacheService.Object)
                       .BuildServiceProvider();

            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null)).Returns("306")
                .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null)).Returns(string.Empty)
               .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null)).Returns(JsonConvert.SerializeObject(contactCreated))
                .Verifiable();

            Func<Task> task = () => Task.CompletedTask;

            await CustomerCreationMiddleWare.InvokeAsync(httpContext, task);

            httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task InvokeAsync_Should_ReturnForbidden_IfAccountNumberInBody_NotValid()
        {

            string htmlPath = "/gtw/account/api/customers?accountId=306";
            string method = HttpMethod.Post.Method;
            var contactCreated = new CreatedContact { AccountNumber = string.Empty };
            var httpContext = Dummies.DummyHttpContext(htmlPath, method, string.Empty, null);
            httpContext.Request.Path = htmlPath;
            httpContext.Request.Method = method;
            httpContext.RequestServices = new ServiceCollection()
                       .AddSingleton(_mockAccountService.Object)
                       .AddSingleton(_mockCacheService.Object)
                       .BuildServiceProvider();

            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null)).Returns("118")
                .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null)).Returns("306")
               .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null)).Returns(JsonConvert.SerializeObject(contactCreated))
                .Verifiable();

            Func<Task> task = () => Task.CompletedTask;

            await CustomerCreationMiddleWare.InvokeAsync(httpContext, task);

            httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }


        [Fact]
        public async Task InvokeAsync_Should_ReturnForbidden_IfAccountNumberInBody_DoesNotExistsInTheContactRole()
        {
            string htmlPath = "/gtw/account/api/customers?accountId=306";
            string method = HttpMethod.Post.Method;
            var contactCreated = new CreatedContact { AccountNumber = "AN_ACCOUNT_NUMBER_THAT_SHOULD_NOT_EXISTS_IN_CONTACT_ROLES" };
            var httpContext = Dummies.DummyHttpContext(htmlPath, method, string.Empty, null);
            httpContext.Request.Path = htmlPath;
            httpContext.Request.Method = method;
            httpContext.RequestServices = new ServiceCollection()
                       .AddSingleton(_mockAccountService.Object)
                       .AddSingleton(_mockCacheService.Object)
                       .BuildServiceProvider();

            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null)).Returns("118")
                .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null)).Returns("306")
               .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null)).Returns(JsonConvert.SerializeObject(contactCreated))
                .Verifiable();

            _mockAccountService.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), null, It.IsAny<string>())).ReturnsAsync(false).Verifiable();

            Func<Task> task = () => Task.CompletedTask;

            await CustomerCreationMiddleWare.InvokeAsync(httpContext, task);

            _mockAccountService.Verify(x => x.CheckContactRoleAsync(It.IsAny<int>(), null, It.IsAny<string>()), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null), Times.Once);
            httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        }


        [Fact]
        public async Task InvokeAsync_Should_ReturnOK_IfAccountNumberInBody_DoesExistsInTheContactRole()
        {
            string htmlPath = "/gtw/account/api/customers?accountId=306";
            string method = HttpMethod.Post.Method;
            var contactCreated = new CreatedContact { AccountNumber = "THIS_SHOULD_EXIST" };
            var httpContext = Dummies.DummyHttpContext(htmlPath, method, string.Empty, null);
            httpContext.Request.Path = htmlPath;
            httpContext.Request.Method = method;
            httpContext.RequestServices = new ServiceCollection()
                       .AddSingleton(_mockAccountService.Object)
                       .AddSingleton(_mockCacheService.Object)
                       .BuildServiceProvider();

            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null)).Returns("118")
                .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null)).Returns("306")
               .Verifiable();
            _mockCacheService.Setup(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null)).Returns(JsonConvert.SerializeObject(contactCreated))
                .Verifiable();
            _mockAccountService.Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), null, It.IsAny<string>())).ReturnsAsync(true).Verifiable();

            Func<Task> task = () => Task.CompletedTask;

            await CustomerCreationMiddleWare.InvokeAsync(httpContext, task);

            _mockAccountService.Verify(x => x.CheckContactRoleAsync(It.IsAny<int>(), null, It.IsAny<string>()), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheContactId, null, null), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheAccountId, null, null), Times.Once);
            _mockCacheService.Verify(x => x.GetOrCreate<string>(GlobalsConstants.cacheContent, null, null), Times.Once);
            httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        }
    }
}
