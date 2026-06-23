using System.Net;
using ApiGateway.Account;
using ApiGateway.Contact;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Services;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for the Prospect create-password experience orchestration service.
/// </summary>
public class CreatePasswordExperienceServiceTests
{
    private readonly Mock<IFeatureFlagService> featureFlagService = new(MockBehavior.Strict);
    private readonly Mock<IAccountService> accountService = new(MockBehavior.Strict);
    private readonly Mock<IContactService> contactService = new(MockBehavior.Strict);
    private readonly Mock<ILogger<CreatePasswordExperienceService>> logger = new();
    private readonly CreatePasswordExperienceService service;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePasswordExperienceServiceTests"/> class.
    /// </summary>
    public CreatePasswordExperienceServiceTests()
    {
        service = new CreatePasswordExperienceService(
            featureFlagService.Object,
            accountService.Object,
            contactService.Object,
            logger.Object);
    }

    /// <summary>
    /// Ensures the disabled feature flag path bypasses Account and calls Contact without entity type.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WhenFeatureFlagDisabled_CallsContactWithoutEntityTypeAndDoesNotCallAccount()
    {
        // Arrange
        var request = BuildRequest(contactId: 42);
        var expectedContactRequest = BuildContactRequest(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        contactService
            .Setup(s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(response);
        accountService.Verify(
            s => s.GetProspectOnlyContactResultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        contactService.Verify(
            s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Ensures the Prospect path sends entityType PROSPECT when Account confirms the contact is prospect-only.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WhenFeatureFlagEnabledAndContactIsProspectOnly_CallsContactWithProspectEntityType()
    {
        // Arrange
        var contactId = 42;
        var request = BuildRequest(contactId);
        var expectedContactRequest = BuildContactRequest(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        SetupFeatureFlagEnabled();
        accountService
            .Setup(s => s.GetProspectOnlyContactResultAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProspectOnlyContactResult.ProspectOnly);
        contactService
            .Setup(s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                "PROSPECT",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(response);
        contactService.Verify(
            s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                "PROSPECT",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Ensures Account 404 or false falls back to the default Contact create-password flow.
    /// </summary>
    /// <param name="accountResult">The Account prospect-only result.</param>
    [Theory]
    [InlineData(ProspectOnlyContactResult.NotFound)]
    [InlineData(ProspectOnlyContactResult.NotProspectOnly)]
    public async Task CreateNewPasswordAsync_WhenFeatureFlagEnabledAndContactIsNotProspectOnly_CallsContactWithoutEntityType(
        ProspectOnlyContactResult accountResult)
    {
        // Arrange
        var contactId = 42;
        var request = BuildRequest(contactId);
        var expectedContactRequest = BuildContactRequest(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        SetupFeatureFlagEnabled();
        accountService
            .Setup(s => s.GetProspectOnlyContactResultAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountResult);
        contactService
            .Setup(s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(response);
        contactService.Verify(
            s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Ensures a missing contact identifier does not call Account and preserves the default flow.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WhenFeatureFlagEnabledAndContactIdMissing_FallsBackWithoutCallingAccount()
    {
        // Arrange
        var request = BuildRequest(contactId: null);
        var expectedContactRequest = BuildContactRequest(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        SetupFeatureFlagEnabled();
        contactService
            .Setup(s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(response);
        accountService.Verify(
            s => s.GetProspectOnlyContactResultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ensures unexpected Account failures bubble up and do not call Contact.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WhenAccountFails_PropagatesFailureAndDoesNotCallContact()
    {
        // Arrange
        var contactId = 42;
        var request = BuildRequest(contactId);
        SetupFeatureFlagEnabled();
        accountService
            .Setup(s => s.GetProspectOnlyContactResultAsync(contactId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Account unavailable"));

        // Act
        Func<Task> act = () => service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        contactService.Verify(
            s => s.CreateNewPasswordAsync(It.IsAny<CreateNewPasswordRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Ensures Contact downstream errors are returned to the caller unchanged by the service.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WhenContactReturnsError_ReturnsDownstreamResponse()
    {
        // Arrange
        var request = BuildRequest(contactId: 42);
        var expectedContactRequest = BuildContactRequest(request);
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        contactService
            .Setup(s => s.CreateNewPasswordAsync(
                It.Is<CreateNewPasswordRequest>(r => Matches(r, expectedContactRequest)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await service.CreateNewPasswordAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(response);
    }

    /// <summary>
    /// Ensures a missing create-password action flag defaults to the Contact create-password behavior.
    /// </summary>
    [Fact]
    public void ToContactRequest_WhenCreatePasswordActionIsNull_DefaultsCreatePasswordActionToTrue()
    {
        // Arrange
        var request = new CreatePasswordExperienceRequest(42, "reset-token", "NewPassword123", null);

        // Act
        var contactRequest = request.ToContactRequest();

        // Assert
        contactRequest.isCreatePasswordAction.Should().BeTrue();
    }

    /// <summary>
    /// Ensures an explicit false create-password action flag is preserved.
    /// </summary>
    [Fact]
    public void ToContactRequest_WhenCreatePasswordActionIsFalse_PreservesFalse()
    {
        // Arrange
        var request = new CreatePasswordExperienceRequest(42, "reset-token", "NewPassword123", false);

        // Act
        var contactRequest = request.ToContactRequest();

        // Assert
        contactRequest.isCreatePasswordAction.Should().BeFalse();
    }

    /// <summary>
    /// Builds a representative create-new-password request.
    /// </summary>
    /// <returns>A create-new-password request.</returns>
    private static CreatePasswordExperienceRequest BuildRequest(int? contactId = 42)
    {
        return new CreatePasswordExperienceRequest(contactId, "reset-token", "NewPassword123", true);
    }

    /// <summary>
    /// Builds the Contact downstream request expected from a Gateway request.
    /// </summary>
    /// <param name="request">The Gateway request.</param>
    /// <returns>The Contact downstream request.</returns>
    private static CreateNewPasswordRequest BuildContactRequest(CreatePasswordExperienceRequest request)
    {
        return new CreateNewPasswordRequest(request.token, request.newPassword, request.isCreatePasswordAction);
    }

    /// <summary>
    /// Checks whether a Contact downstream request matches the expected payload.
    /// </summary>
    /// <param name="actual">The actual request.</param>
    /// <param name="expected">The expected request.</param>
    /// <returns><c>true</c> when the payloads match; otherwise, <c>false</c>.</returns>
    private static bool Matches(CreateNewPasswordRequest actual, CreateNewPasswordRequest expected)
    {
        return actual.token == expected.token
            && actual.newPassword == expected.newPassword
            && actual.isCreatePasswordAction == expected.isCreatePasswordAction;
    }

    /// <summary>
    /// Configures the Prospect experience feature flag as enabled.
    /// </summary>
    private void SetupFeatureFlagEnabled()
    {
        featureFlagService
            .Setup(s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}
