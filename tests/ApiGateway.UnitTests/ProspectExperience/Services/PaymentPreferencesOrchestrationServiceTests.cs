using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="PaymentPreferencesOrchestrationService"/>.
/// </summary>
public sealed class PaymentPreferencesOrchestrationServiceTests
{
    private readonly Mock<IProspectApiClient> _prospectClient = new();
    private readonly Mock<IMandatePaymentPreferencesClient> _mandateClient = new();
    private readonly Mock<IProspectService> _prospectService = new();
    private readonly PaymentPreferencesOrchestrationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentPreferencesOrchestrationServiceTests"/> class.
    /// </summary>
    public PaymentPreferencesOrchestrationServiceTests()
    {
        _service = new PaymentPreferencesOrchestrationService(
            _prospectClient.Object,
            _mandateClient.Object,
            _prospectService.Object,
            NullLogger<PaymentPreferencesOrchestrationService>.Instance);
    }

    /// <summary>
    /// Verifies that GET retrieves the prospect account before calling Mandat.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenProspectExists_ReturnsMandatPaymentPreference()
    {
        var expected = new PaymentPreferenceResponse { PaymentType = "OTHER" };
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetAsync(10, CancellationToken.None);

        result.Should().BeSameAs(expected);
        _mandateClient.Verify(client => client.GetAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that GET stops when the prospect cannot be resolved.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenProspectDoesNotExist_ReturnsNull()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectAccountResponse?)null);

        var result = await _service.GetAsync(10, CancellationToken.None);

        result.Should().BeNull();
        _mandateClient.Verify(client => client.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER completes the Prospect payment method step after Mandat succeeds.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenMandatSucceeds_CompletesPaymentMethodStep()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.SetOtherAsync(42, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectService
            .Setup(service => service.CompleteStepAsync(
                10,
                It.Is<CompleteStepRequest>(request => request.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                7))
            .ReturnsAsync(new DocumentUploadResultResponse([], []));

        var result = await _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        result.Should().BeTrue();
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                10,
                It.Is<CompleteStepRequest>(request => request.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                7),
            Times.Once);
    }

    /// <summary>
    /// Verifies that POST OTHER does not complete Prospect when Mandat fails.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenMandatFails_DoesNotCompletePaymentMethodStep()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.SetOtherAsync(42, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        result.Should().BeFalse();
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER stops when the prospect cannot be resolved.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenProspectDoesNotExist_ReturnsFalse()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectAccountResponse?)null);

        var result = await _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        result.Should().BeFalse();
        _mandateClient.Verify(
            client => client.SetOtherAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that DELETE resets the Prospect payment method step after Mandat succeeds.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenMandatSucceeds_ResetsPaymentMethodStep()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.ResetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.ResetStepAsync(10, "PAYMENT_METHOD", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ResetAsync(10, CancellationToken.None);

        result.Should().BeTrue();
        _mandateClient.Verify(client => client.ResetAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _prospectClient.Verify(
            client => client.ResetStepAsync(10, "PAYMENT_METHOD", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that DELETE does not reset Prospect when Mandat fails.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenMandatFails_DoesNotResetPaymentMethodStep()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.ResetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ResetAsync(10, CancellationToken.None);

        result.Should().BeFalse();
        _prospectClient.Verify(
            client => client.ResetStepAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that DELETE stops when the prospect cannot be resolved.
    /// </summary>
    [Fact]
    public async Task ResetAsync_WhenProspectDoesNotExist_ReturnsFalse()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectAccountResponse?)null);

        var result = await _service.ResetAsync(10, CancellationToken.None);

        result.Should().BeFalse();
        _mandateClient.Verify(
            client => client.ResetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
