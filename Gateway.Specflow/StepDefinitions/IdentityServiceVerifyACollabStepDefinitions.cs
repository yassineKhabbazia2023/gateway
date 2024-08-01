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
    [Binding, Scope(Feature = "IdentityService verify a collab")]
    public class IdentityServiceVerifyACollabStepDefinitions
    {
        private DefaultHttpContext? _context;
        private Mock<IIdentityService> _mockIdentityService = new Mock<IIdentityService>();
        private Mock<IContactService> _mockContactService = new Mock<IContactService>();
        private Mock<IAuthorizationSevice> _mockAuthorizationService = new Mock<IAuthorizationSevice>();
        private Mock<IAccountService> _mockAccountService = new Mock<IAccountService>();

        [Given("collab exists in group")]
        public void GivenCollabExistsInGroup()
        {
            _mockIdentityService.Setup(x => x.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(true);
        }

        [Given("collab not exists in group")]
        public void GivenCollabNotExistsInGroup()
        {
            _mockIdentityService.Setup(x => x.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(false);
        }

        [When("IdentityService verify this collab")]
        public async Task WhenIdentityServiceVerifyThisCollab()
        {
            var path = "/gtw/authorization/api/accounts/1";
            var method = "GET";
            var requiredClaims = new Dictionary<string, string>();

            _context = HttpContextHelper.DummyHttpContext(path, method, string.Empty, requiredClaims, true);
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
