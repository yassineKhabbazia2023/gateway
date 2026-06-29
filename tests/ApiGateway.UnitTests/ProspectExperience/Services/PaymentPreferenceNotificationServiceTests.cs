using ApiGateway.ProspectExperience.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="PaymentPreferenceNotificationService"/>.
/// </summary>
public sealed class PaymentPreferenceNotificationServiceTests
{
    private readonly Mock<IProspectApiClient> _prospectClient = new();

    #region GetCollabEmailReceivers

    /// <summary>
    /// Verifies that one configured receiver is returned as a single-item array.
    /// </summary>
    [Fact]
    public void GetCollabEmailReceivers_WhenConfigurationContainsOneEmail_ReturnsOneReceiver()
    {
        var service = CreateService("bs@test.fr");

        var result = service.GetCollabEmailReceivers();

        result.Should().Equal("bs@test.fr");
    }

    /// <summary>
    /// Verifies that semicolon-separated receivers are returned as multiple items.
    /// </summary>
    [Fact]
    public void GetCollabEmailReceivers_WhenConfigurationContainsMultipleEmails_ReturnsReceivers()
    {
        var service = CreateService("bs1@test.fr;bs2@test.fr");

        var result = service.GetCollabEmailReceivers();

        result.Should().Equal("bs1@test.fr", "bs2@test.fr");
    }

    /// <summary>
    /// Verifies that empty entries are ignored.
    /// </summary>
    [Fact]
    public void GetCollabEmailReceivers_WhenConfigurationContainsEmptyEntries_IgnoresEmptyEntries()
    {
        var service = CreateService("bs1@test.fr;; ;bs2@test.fr;");

        var result = service.GetCollabEmailReceivers();

        result.Should().Equal("bs1@test.fr", "bs2@test.fr");
    }

    /// <summary>
    /// Verifies that empty configuration returns an empty receiver array.
    /// </summary>
    [Fact]
    public void GetCollabEmailReceivers_WhenConfigurationIsEmpty_ReturnsEmptyArray()
    {
        var service = CreateService(string.Empty);

        var result = service.GetCollabEmailReceivers();

        result.Should().BeEmpty();
    }

    #endregion

    #region SendAsync

    /// <summary>
    /// Verifies that the Prospect notification endpoint receives the expected targets.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenCalled_ForwardsTargetsToProspect()
    {
        var service = CreateService("bs@test.fr");

        await service.SendAsync(10, "signatory@test.fr", ["bs@test.fr"], CancellationToken.None);

        _prospectClient.Verify(client => client.SendPaymentPreferenceNotificationsAsync(
            10,
            "signatory@test.fr",
            It.Is<string[]>(receivers => receivers.SequenceEqual(new[] { "bs@test.fr" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that Prospect notification failures are logged and do not escape the service.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenProspectCallFails_DoesNotThrow()
    {
        _prospectClient
            .Setup(client => client.SendPaymentPreferenceNotificationsAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Prospect unavailable"));
        var service = CreateService("bs@test.fr");

        Func<Task> act = () => service.SendAsync(10, "signatory@test.fr", ["bs@test.fr"], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    #endregion

    /// <summary>
    /// Creates the tested service.
    /// </summary>
    /// <param name="receivers">The configured receivers value.</param>
    /// <returns>The tested service.</returns>
    private PaymentPreferenceNotificationService CreateService(string receivers)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentPreference:Notifications:Receivers"] = receivers
            })
            .Build();

        return new PaymentPreferenceNotificationService(
            _prospectClient.Object,
            configuration,
            NullLogger<PaymentPreferenceNotificationService>.Instance);
    }
}
