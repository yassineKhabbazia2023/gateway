using ApiGateway.Account;
using ApiGateway.ConnectExperience.Controller;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Contact;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Contact;

public class ConnectControllerTests
{
    private readonly Mock<IConnectServices> _experienceServices;
    private readonly Mock<IAccountService> _accountService;
    private readonly Mock<IContactService> _contactService;
    private readonly int _contactId;
    private readonly int _accountId;
    private readonly ConnectController _sut;

    private class TestClaimsPrincipal : ClaimsPrincipal
    {
        public override Claim? FindFirst(string type)
        {
            return new Claim(ClaimValueTypes.Email, "bob@truc.io");
        }
    }

    private class TestUserContext : IUserContext
    {
        public ClaimsPrincipal User => new TestClaimsPrincipal();
    }

    public ConnectControllerTests()
    {
        _experienceServices = new Mock<IConnectServices>();
        _accountService = new Mock<IAccountService>();
        _contactService = new Mock<IContactService>();
        _contactId = 123456;
        _contactService.Setup(s => s.GetContactAsync("bob@truc.io")).ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = _contactId });
        _accountId = 316354;
        _sut = new ConnectController(new TestUserContext(), _experienceServices.Object, _accountService.Object, _contactService.Object);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnOk_WhenContactHasRoleOnAccount()
    {
        _accountService.Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null)).ReturnsAsync(true);
        _experienceServices.Setup(s => s.GetSummaryAsync(_accountId)).ReturnsAsync(new ApiGateway.Models.Summary());
        
        var result = await _sut.GetSummary(_accountId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSummary_ShouldReturnOk_WhenContactHasNoRoleOnAccount()
    {
        _accountService.Setup(s => s.CheckContactRoleAsync(_contactId, _accountId, null)).ReturnsAsync(false);

        var result = await _sut.GetSummary(_accountId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
