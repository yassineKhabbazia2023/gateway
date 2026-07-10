using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Helpers;
using ContactModel = ApiGateway.Contact.Models.Contact;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.UnitTests.ProspectExperience.Helpers;

/// <summary>
/// Unit tests for <see cref="ProspectAccountAuthorizationHelper"/>.
/// </summary>
public sealed class ProspectAccountAuthorizationHelperTests
{
    private const int AccountId = 602;
    private const int ContactId = 12;
    private const string ContactEmail = "user@test.fr";

    private readonly Mock<IContactService> _contactService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IAccountService> _accountService = new();

    /// <summary>
    /// Verifies that authorization returns not found when the connected contact cannot be resolved.
    /// </summary>
    [Fact]
    public async Task AuthorizeAccountRoleAsync_WhenContactIsMissing_ReturnsNotFound()
    {
        _contactService
            .Setup(service => service.GetContactAsync(ContactEmail))
            .ReturnsAsync((ContactModel?)null);

        var result = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(
            AccountId,
            ContactEmail,
            _contactService.Object,
            CreateHttpContext());

        result.Error.Should().BeOfType<NotFoundResult>();
        result.Contact.Should().BeNull();
        _authorizationService.Verify(
            service => service.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()),
            Times.Never);
        _accountService.Verify(
            service => service.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that authorization returns the contact without account checks when the connected contact can skip role validation.
    /// </summary>
    [Fact]
    public async Task AuthorizeAccountRoleAsync_WhenRoleCheckShouldBeSkipped_ReturnsContact()
    {
        var contact = SetupContact();
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(ContactId, AccountId))
            .ReturnsAsync(new List<string> { "COADMI004" });

        var result = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(
            AccountId,
            ContactEmail,
            _contactService.Object,
            CreateHttpContext());

        result.Error.Should().BeNull();
        result.Contact.Should().BeSameAs(contact);
        _accountService.Verify(
            service => service.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that authorization returns the standard no-role error when the connected contact has no role on the account.
    /// </summary>
    [Fact]
    public async Task AuthorizeAccountRoleAsync_WhenContactHasNoAccountRole_ReturnsForbidden()
    {
        SetupContact();
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(ContactId, AccountId))
            .ReturnsAsync(new List<string>());
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(ContactId, -1))
            .ReturnsAsync(new List<string>());
        _accountService
            .Setup(service => service.CheckContactRoleAsync(ContactId, AccountId, null))
            .ReturnsAsync(false);

        var result = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(
            AccountId,
            ContactEmail,
            _contactService.Object,
            CreateHttpContext());

        var objectResult = result.Error.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            ErrorCode = Errors.NoRoleOnAccountCode,
            ErrorMessage = string.Format(Errors.NoRoleOnAccountMessage, ContactId, AccountId)
        });
        result.Contact.Should().BeNull();
    }

    /// <summary>
    /// Verifies that authorization returns the contact when the connected contact has a role on the account.
    /// </summary>
    [Fact]
    public async Task AuthorizeAccountRoleAsync_WhenContactHasAccountRole_ReturnsContact()
    {
        var contact = SetupContact();
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(ContactId, AccountId))
            .ReturnsAsync(new List<string>());
        _authorizationService
            .Setup(service => service.GetContactAuthorizationAsync(ContactId, -1))
            .ReturnsAsync(new List<string>());
        _accountService
            .Setup(service => service.CheckContactRoleAsync(ContactId, AccountId, null))
            .ReturnsAsync(true);

        var result = await ProspectAccountAuthorizationHelper.AuthorizeAccountRoleAsync(
            AccountId,
            ContactEmail,
            _contactService.Object,
            CreateHttpContext());

        result.Error.Should().BeNull();
        result.Contact.Should().BeSameAs(contact);
    }

    /// <summary>
    /// Creates an HTTP context containing authorization dependencies.
    /// </summary>
    /// <returns>The HTTP context.</returns>
    private HttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(_authorizationService.Object)
                .AddSingleton(_accountService.Object)
                .BuildServiceProvider()
        };
    }

    /// <summary>
    /// Configures the connected contact lookup.
    /// </summary>
    /// <returns>The configured contact.</returns>
    private ContactModel SetupContact()
    {
        var contact = new ContactModel
        {
            Id = ContactId,
            Email = ContactEmail
        };

        _contactService
            .Setup(service => service.GetContactAsync(ContactEmail))
            .ReturnsAsync(contact);

        return contact;
    }
}
