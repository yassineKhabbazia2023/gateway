using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Models;
using Pulse.ExceptionMiddleware.Exceptions;
using ContactModel = ApiGateway.Contact.Models.Contact;

namespace ApiGateway.UnitTests.ConnectExperience;

public class ExperienceServicesTests
{
    private readonly Mock<IContactService> _contactMock = new();
    private readonly Mock<IAuthorizationService> _authMock = new();
    private readonly Mock<IAccountService> _accountMock = new();

    private ConnectServices CreateService() =>
        new(_contactMock.Object, _authMock.Object, _accountMock.Object);

    [Fact]
    public async Task GetUserInformation_ShouldReturnUserInfo_WhenContactExists()
    {
        // Arrange
        var contact = new ContactModel
        {
            Id = 123,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LandPhone = "123456",
            MobilePhone = "789101",
            OldId = "ABC123",
            Type = "Client",
            Persona = new ApiGateway.Contact.Models.Persona(1, "Customer")
        };

        var authorizations = new[] { "READ", "WRITE" };
        IReadOnlyCollection<FavoriteAccount> favorites = [
            new FavoriteAccount { AccountId = 1, AccountNumber = "accountNumber1", LegalName = "legalName1"},
            new FavoriteAccount { AccountId = 2, AccountNumber = "accountNumber2", LegalName = "legalName2"},
        ];

        _contactMock.Setup(x => x.GetContactAsync(contact.Email)).ReturnsAsync(contact);
        _authMock.Setup(x => x.GetContactAuthorizationAsync(contact.Id, null)).Returns(Task.FromResult(authorizations.ToList()));
        _accountMock.Setup(x => x.GetFavoriteAccountsByContactIdAsync(contact.Id)).Returns(Task.FromResult(favorites)!);

        var service = CreateService();

        // Act
        var result = await service.GetUserInformation(contact.Email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contact.Id, result.Contact.Id);
        Assert.Equal(authorizations, result.Permissions);
        Assert.Equal(favorites, result.FavoriteEntities);
    }

    [Fact]
    public async Task GetUserInformation_ShouldThrowBadRequest_WhenContactIsNull()
    {
        // Arrange
        var email = "notfound@example.com";
        _contactMock.Setup(x => x.GetContactAsync(email)).ReturnsAsync((ContactModel)null!);

        var service = CreateService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => service.GetUserInformation(email));

        Assert.Equal(Errors.NotFoundContactCode, ex.Code);
        Assert.Equal(Errors.NotFoundContactMessage, ex.Message);
    }
}