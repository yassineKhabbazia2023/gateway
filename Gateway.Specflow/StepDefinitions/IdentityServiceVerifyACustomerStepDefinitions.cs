using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Identity;
using ApiGateway.Middlewares;
using Gateway.Specflow.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Gateway.Specflow.StepDefinitions
{
    [Binding, Scope(Feature = "IdentityService verify a customer")]
    public class IdentityServiceVerifyACustomerStepDefinitions
    {
        private DefaultHttpContext? _context;
        private Mock<IIdentityService> _mockIdentityService = new Mock<IIdentityService>();
        private Mock<IContactService> _mockContactService = new Mock<IContactService>();
        private Mock<IAuthorizationSevice> _mockAuthorizationService = new Mock<IAuthorizationSevice>();
        private Mock<IAccountService> _mockAccountService = new Mock<IAccountService>();

        [Given("customer exists in Gigya")]
        public void GivenCustomerExistsInGigya()
        {
            _mockIdentityService.Setup(x => x.ValidateCustomerAsync(It.IsAny<string>())).ReturnsAsync(true);
        }

        [Given("customer not exists in Gigya")]
        public void GivenCustomerNotExistsInGigya()
        {
            _mockIdentityService.Setup(x => x.ValidateCustomerAsync(It.IsAny<string>())).ReturnsAsync(false);
        }

        [When("IdentityService verify this customer")]
        public async Task WhenIdentityServiceVerifyThisCustomer()
        {
            var path = "/gtw/authorization/api/accounts/1";
            var method = "GET";
            var requiredClaims = new Dictionary<string, string>();

            _context = HttpContextHelper.DummyHttpContext(path, method, string.Empty, requiredClaims);
            _context.RequestServices = new ServiceCollection()
                .AddSingleton(_mockContactService.Object)
                .AddSingleton(_mockAuthorizationService.Object)
                .AddSingleton(_mockAccountService.Object)
                .AddSingleton(_mockIdentityService.Object)
                .BuildServiceProvider();

            await AuthorizationMiddleware.AuthorizationFilter(_context!, () => Task.CompletedTask);
        }

        [Then("It return code {int}")]
        public void ThenItReturnCode(int p0)
        {
            Assert.Equal(p0, _context!.Response.StatusCode);
        }
    }
}
