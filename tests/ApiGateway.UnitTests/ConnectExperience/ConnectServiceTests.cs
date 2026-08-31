using ApiGateway.Account;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
using ApiGateway.ConnectExperience.Services;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Models;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Exceptions;
using ContactModel = ApiGateway.Contact.Models.Contact;
using ApiGateway.Offer;
using ApiGateway.Offer.Constants;

namespace ApiGateway.UnitTests.ConnectExperience;

public class ExperienceServicesTests
{
    private readonly Mock<IContactService> _contactMock = new();
    private readonly Mock<IAuthorizationService> _authMock = new();
    private readonly Mock<IAccountService> _accountMock = new();
    private readonly Mock<IOfferService> _offerMock = new();
    private readonly Mock<IFeatureFlagService> _featureFlagMock = new();
    private readonly Mock<ILogger<ConnectServices>> _loggerMock = new();
    private ConnectServices CreateService() =>
        new(_contactMock.Object, _authMock.Object, _offerMock.Object, _accountMock.Object, _featureFlagMock.Object, _loggerMock.Object);
    private readonly string _collabType = "Collaborator";

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
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId, _collabType)).ReturnsAsync((Summary?)null);
        var service = CreateService();

        var result = async () => await service.GetSummaryAsync(accountNumber, contactId, _collabType);

        await result.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldThrowBadRequest_WhenContactIsNotFoundOnOffer()
    {
        var accountNumber = 876;
        var contactId = 123;
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId, _collabType)).ReturnsAsync(new Summary());
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountNumber)).ReturnsAsync((SubscriptionStatus[]?)null);
        var service = CreateService();

        var result = async () => await service.GetSummaryAsync(accountNumber, contactId, _collabType);
        await result.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldNotReturnNull_ToConfirm()
    {
        var account = new Fixture().Create<Summary>();
        var accountNumber = 876;
        var contactId = 123;
        _accountMock.Setup(s => s.GetSummaryAsync(accountNumber, contactId, _collabType)).ReturnsAsync(account);
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountNumber)).ReturnsAsync([]);
        var service = CreateService();

        var result = await service.GetSummaryAsync(accountNumber, contactId, _collabType);
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

        var summaryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptionsStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCalls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _accountMock
            .Setup(s => s.GetSummaryAsync(accountId, contactId, _collabType))
            .Returns(async () =>
            {
                summaryStarted.SetResult(true);
                await subscriptionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await releaseCalls.Task;
                return expectedSummary;
            });

        _offerMock
            .Setup(s => s.GetSubscriptionsAsync(accountId))
            .Returns(async () =>
            {
                subscriptionsStarted.SetResult(true);
                await summaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await releaseCalls.Task;
                return expectedSubscriptions;
            });

        var service = CreateService();

        // Act
        var resultTask = service.GetSummaryAsync(accountId, contactId, _collabType);
        await Task.WhenAll(summaryStarted.Task, subscriptionsStarted.Task).WaitAsync(TimeSpan.FromSeconds(1));
        releaseCalls.SetResult(true);
        var result = await resultTask;

        // Assert
        result.Should().NotBeNull();
        result!.Subscriptions.Should().BeEquivalentTo(expectedSubscriptions);

        // Verify both services were called
        _accountMock.Verify(s => s.GetSummaryAsync(accountId, contactId, _collabType), Times.Once);
        _offerMock.Verify(s => s.GetSubscriptionsAsync(accountId), Times.Once);

        // Verify calls were made in parallel by requiring both calls to start before either one can complete.
        summaryStarted.Task.IsCompletedSuccessfully.Should().BeTrue();
        subscriptionsStarted.Task.IsCompletedSuccessfully.Should().BeTrue();
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

        _accountMock.Setup(s => s.GetSummaryAsync(accountId, contactId, _collabType)).ReturnsAsync(expectedSummary);
        _offerMock.Setup(s => s.GetSubscriptionsAsync(accountId)).ReturnsAsync(expectedSubscriptions);

        var service = CreateService();

        // Act
        var result = await service.GetSummaryAsync(accountId, contactId, _collabType);

        // Assert
        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be(expectedSummary.AccountNumber);
        result.LegalName.Should().Be(expectedSummary.LegalName);
        result.Subscriptions.Should().HaveCount(2);
        result.Subscriptions.Should().BeEquivalentTo(expectedSubscriptions);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldSendInvitesAndUpdateLastActivity_WhenContactAndAccountExist()
    {
        // Arrange
        const string userEmail = "john@example.com";
        const int accountId = 42;
        var customerIds = new[] { 10, 20 };
        const string entityType = "PROSPECT";

        var contact = new ContactModel { Id = 123, Type = "Collaborator" };
        var account = new ApiGateway.Models.Account { AccountId = accountId, AccountNumber = "0000000042" };
        var invitedIds = new[] { 10, 20 };

        _contactMock.Setup(x => x.GetContactAsync(userEmail)).ReturnsAsync(contact);
        _accountMock.Setup(x => x.GetAccountAsync(accountId)).ReturnsAsync(account);
        _featureFlagMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _contactMock
            .Setup(x => x.SendEmailAsync(contact.Id, account.AccountNumber!, customerIds, entityType))
            .ReturnsAsync(invitedIds)
            .Verifiable();
        _accountMock
            .Setup(x => x.UpdateLastActivityDateAsync(contact.Id, contact.Type!, accountId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService();

        // Act
        var result = await service.SendEmailAsync(userEmail, accountId, customerIds, entityType);

        // Assert
        result.Should().BeEquivalentTo(invitedIds);
        _contactMock.Verify(x => x.GetContactAsync(userEmail), Times.Once);
        _accountMock.Verify(x => x.GetAccountAsync(accountId), Times.Once);
        _contactMock.Verify();
        _accountMock.Verify();
    }

    [Fact]
    public async Task SendEmailAsync_ShouldThrowForbidden_WhenProspectEntityTypeRequestedAndFlagDisabled()
    {
        // Arrange
        const string userEmail = "john@example.com";
        const int accountId = 42;
        var customerIds = new[] { 10, 20 };
        const string entityType = "PROSPECT";

        var contact = new ContactModel { Id = 123, Type = "Collaborator" };
        var account = new ApiGateway.Models.Account { AccountId = accountId, AccountNumber = "0000000042" };

        _contactMock.Setup(x => x.GetContactAsync(userEmail)).ReturnsAsync(contact);
        _accountMock.Setup(x => x.GetAccountAsync(accountId)).ReturnsAsync(account);
        _featureFlagMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var act = async () => await service.SendEmailAsync(userEmail, accountId, customerIds, entityType);

        // Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(act);
        ex.Code.Should().Be(Errors.ProspectExperienceDisabledCode);
        ex.Message.Should().Be(Errors.ProspectExperienceDisabledMessage);
        _contactMock.Verify(x => x.SendEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int[]>(), It.IsAny<string?>()), Times.Never);
        _accountMock.Verify(x => x.UpdateLastActivityDateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldSendInvites_WhenEntityTypeIsNotProspectAndFlagDisabled()
    {
        // Arrange
        const string userEmail = "john@example.com";
        const int accountId = 42;
        var customerIds = new[] { 10, 20 };

        var contact = new ContactModel { Id = 123, Type = "Collaborator" };
        var account = new ApiGateway.Models.Account { AccountId = accountId, AccountNumber = "0000000042" };
        var invitedIds = new[] { 10, 20 };

        _contactMock.Setup(x => x.GetContactAsync(userEmail)).ReturnsAsync(contact);
        _accountMock.Setup(x => x.GetAccountAsync(accountId)).ReturnsAsync(account);
        _featureFlagMock
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _contactMock
            .Setup(x => x.SendEmailAsync(contact.Id, account.AccountNumber!, customerIds, null))
            .ReturnsAsync(invitedIds);
        _accountMock
            .Setup(x => x.UpdateLastActivityDateAsync(contact.Id, contact.Type!, accountId))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.SendEmailAsync(userEmail, accountId, customerIds, entityType: null);

        // Assert
        result.Should().BeEquivalentTo(invitedIds);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldThrowBadRequest_WhenContactNotFound()
    {
        // Arrange
        const string userEmail = "missing@example.com";

        _contactMock.Setup(x => x.GetContactAsync(userEmail)).ReturnsAsync((ContactModel)null!);
        var service = CreateService();

        // Act
        var act = async () => await service.SendEmailAsync(userEmail, 42, [10], "PROSPECT");

        // Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(act);
        ex.Code.Should().Be(Errors.NotFoundContactCode);
        ex.Message.Should().Be(Errors.NotFoundContactMessage);
        _accountMock.Verify(x => x.GetAccountAsync(It.IsAny<int>()), Times.Never);
        _contactMock.Verify(x => x.SendEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int[]>(), It.IsAny<string?>()), Times.Never);
        _accountMock.Verify(x => x.UpdateLastActivityDateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldThrowBadRequest_WhenAccountNotFound()
    {
        // Arrange
        const string userEmail = "john@example.com";
        const int accountId = 42;
        var contact = new ContactModel { Id = 123, Type = "Collaborator" };

        _contactMock.Setup(x => x.GetContactAsync(userEmail)).ReturnsAsync(contact);
        _accountMock.Setup(x => x.GetAccountAsync(accountId)).ReturnsAsync((ApiGateway.Models.Account)null!);
        var service = CreateService();

        // Act
        var act = async () => await service.SendEmailAsync(userEmail, accountId, [10], "PROSPECT");

        // Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(act);
        ex.Code.Should().Be(Errors.NotFoundAccountCode);
        ex.Message.Should().Be(string.Format(Errors.NotFoundAccountMessage, accountId));
        _contactMock.Verify(x => x.SendEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int[]>(), It.IsAny<string?>()), Times.Never);
        _accountMock.Verify(x => x.UpdateLastActivityDateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    // ---------------------------------------------------------------------------------------------
    // Modal Serenite : table de verite. La decision est vraie des qu'UNE entite du portefeuille
    // remplit tous les criteres. Le critere 3 (souscription Pennylane) est evalue par Offer.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    // aucune entite souscrite => la modal s'affiche
    [InlineData(false, new[] { 10 }, new int[0], true)]
    // 10 est souscrite mais 20 reste eligible => la modal s'affiche
    [InlineData(false, new[] { 10, 20 }, new[] { 10 }, true)]
    // la seule candidate est souscrite => pas de modal
    [InlineData(false, new[] { 10 }, new[] { 10 }, false)]
    // toutes les candidates sont souscrites => pas de modal
    [InlineData(false, new[] { 10, 20 }, new[] { 10, 20 }, false)]
    // aucune entite candidate => pas de modal
    [InlineData(false, new int[0], new int[0], false)]
    // un choix a deja ete exprime => pas de modal
    [InlineData(true, new int[0], new int[0], false)]
    public async Task GetShouldDisplaySerenityModalAsync_ShouldApplyEveryCriterion(
        bool hasMadeChoice,
        int[] candidateAccountIds,
        int[] subscribedAccountIds,
        bool expected)
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId))
            .ReturnsAsync(new SerenityEligibility { HasMadeChoice = hasMadeChoice, CandidateAccountIds = candidateAccountIds });
        _offerMock.Setup(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), OfferCodes.Pennylane))
            .ReturnsAsync(subscribedAccountIds);

        var service = CreateService();

        // Act
        var result = await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetShouldDisplaySerenityModalAsync_WhenChoiceAlreadyMade_ShouldNotCallOffer()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId))
            .ReturnsAsync(new SerenityEligibility { HasMadeChoice = true, CandidateAccountIds = [] });

        var service = CreateService();

        // Act
        var result = await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        result.Should().BeFalse();
        _offerMock.Verify(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetShouldDisplaySerenityModalAsync_WhenPortfolioHasNoCandidate_ShouldNotCallOffer()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId))
            .ReturnsAsync(new SerenityEligibility { HasMadeChoice = false, CandidateAccountIds = [] });

        var service = CreateService();

        // Act
        var result = await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        result.Should().BeFalse();
        _offerMock.Verify(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetShouldDisplaySerenityModalAsync_WhenAccountUnreachable_ShouldReturnFalse()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId)).ReturnsAsync((SerenityEligibility?)null);

        var service = CreateService();

        // Act
        var result = await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        result.Should().BeFalse();
        _offerMock.Verify(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetShouldDisplaySerenityModalAsync_WhenOfferUnreachable_ShouldFailClosed()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId))
            .ReturnsAsync(new SerenityEligibility { HasMadeChoice = false, CandidateAccountIds = [10] });
        _offerMock.Setup(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), OfferCodes.Pennylane))
            .ReturnsAsync((int[]?)null);

        var service = CreateService();

        // Act
        var result = await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetShouldDisplaySerenityModalAsync_ShouldQueryOfferWithTheCandidatesOnly()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.GetSerenityEligibilityAsync(contactId))
            .ReturnsAsync(new SerenityEligibility { HasMadeChoice = false, CandidateAccountIds = [10, 20] });
        _offerMock.Setup(x => x.GetAccountIdsWithActiveSubscriptionAsync(It.IsAny<int[]>(), It.IsAny<string>()))
            .ReturnsAsync([]);

        var service = CreateService();

        // Act
        await service.GetShouldDisplaySerenityModalAsync(contactId);

        // Assert
        _offerMock.Verify(
            x => x.GetAccountIdsWithActiveSubscriptionAsync(
                It.Is<int[]>(ids => ids.SequenceEqual(new[] { 10, 20 })),
                OfferCodes.Pennylane),
            Times.Once);
    }

    [Fact]
    public async Task SetSerenityModalChoiceAsync_ShouldDelegateToAccountService()
    {
        // Arrange
        const int contactId = 42;
        _accountMock.Setup(x => x.SetSerenityChoiceAsync(contactId, true)).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.SetSerenityModalChoiceAsync(contactId, true);

        // Assert
        _accountMock.Verify(x => x.SetSerenityChoiceAsync(contactId, true), Times.Once);
    }
}
