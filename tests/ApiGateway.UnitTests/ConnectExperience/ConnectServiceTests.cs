using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.Models;
using Pulse.ExceptionMiddleware.Exceptions;
using ContactModel = ApiGateway.Contact.Models.Contact;
using ApiGateway.Offer;

namespace ApiGateway.UnitTests.ConnectExperience;

public class ExperienceServicesTests
{
    private readonly Mock<IContactService> _contactMock = new();
    private readonly Mock<IAuthorizationService> _authMock = new();
    private readonly Mock<IAccountService> _accountMock = new();
    private readonly Mock<IOfferService> _offerMock = new();
    private ConnectServices CreateService() =>
        new(_contactMock.Object, _authMock.Object, _offerMock.Object, _accountMock.Object);

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

    [Fact]
    public async Task GetSummaryAsync_ShouldThrowBadRequest_WhenContactIsNotFoundOnAccount()
    {
        var accountNumber = 324;
        var contactId = 123;
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId)).ReturnsAsync((Summary?)null);
        var service = CreateService();

        var result = async () => await service.GetSummaryAsync(accountNumber, contactId);

        await result.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldThrowBadRequest_WhenContactIsNotFoundOnOffer()
    {
        var accountNumber = 876;
        var contactId = 123;
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId)).ReturnsAsync(new Summary());
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountNumber)).ReturnsAsync((SubscriptionStatus[]?)null);
        var service = CreateService();

        var result = async () => await service.GetSummaryAsync(accountNumber, contactId);

        await result.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldNotReturnNull_ToConfirm()
    {
        var account = new Fixture().Create<Summary>();
        var accountNumber = 876;
        var contactId = 123;
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId)).ReturnsAsync(account);
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountNumber)).ReturnsAsync([]);
        var service = CreateService();

        var result = await service.GetSummaryAsync(accountNumber, contactId);

        result.Should().BeEquivalentTo(account);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCallBothServicesInParallel()
    {
        // Arrange
        var accountId = 123;
        var contactId = 456;
        var expectedSummary = new Fixture().Create<Summary>();
        var expectedSubscriptions = new[] { new SubscriptionStatus { OfferCode = "SUB1" } };

        var summaryCallTime = DateTime.MinValue;
        var subscriptionsCallTime = DateTime.MinValue;

        _accountMock
            .Setup(s => s.GetSummaryAsync(accountId, contactId))
            .Returns(async () =>
            {
                summaryCallTime = DateTime.UtcNow;
                await Task.Delay(50); // Simulate network latency
                return expectedSummary;
            });

        _offerMock
            .Setup(s => s.GetSubscriptionsAsync(accountId))
            .Returns(async () =>
            {
                subscriptionsCallTime = DateTime.UtcNow;
                await Task.Delay(50); // Simulate network latency
                return expectedSubscriptions;
            });

        var service = CreateService();

        // Act
        var result = await service.GetSummaryAsync(accountId, contactId);

        // Assert
        result.Should().NotBeNull();
        result!.Subscriptions.Should().BeEquivalentTo(expectedSubscriptions);

        // Verify both services were called
        _accountMock.Verify(s => s.GetSummaryAsync(accountId, contactId), Times.Once);
        _offerMock.Verify(s => s.GetSubscriptionsAsync(accountId), Times.Once);

        // Verify calls were made in parallel (within 20ms of each other)
        var timeDifference = Math.Abs((summaryCallTime - subscriptionsCallTime).TotalMilliseconds);
        timeDifference.Should().BeLessThan(20, "Both calls should start approximately at the same time (parallel execution)");
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnSummaryWithSubscriptions_WhenBothCallsSucceed()
    {
        // Arrange
        var accountId = 789;
        var contactId = 101;
        var expectedSummary = new Summary
        {
            AccountNumber = "ACC123",
            LegalName = "Test Company"
        };
        var expectedSubscriptions = new[]
        {
            new SubscriptionStatus { OfferCode = "PREMIUM", OfferName = "Premium Plan" },
            new SubscriptionStatus { OfferCode = "BASIC", OfferName = "Basic Plan" }
        };

        _accountMock.Setup(s => s.GetSummaryAsync(accountId, contactId)).ReturnsAsync(expectedSummary);
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountId)).ReturnsAsync(expectedSubscriptions);

        var service = CreateService();

        // Act
        var result = await service.GetSummaryAsync(accountId, contactId);

        // Assert
        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be(expectedSummary.AccountNumber);
        result.LegalName.Should().Be(expectedSummary.LegalName);
        result.Subscriptions.Should().HaveCount(2);
        result.Subscriptions.Should().BeEquivalentTo(expectedSubscriptions);
    }
}