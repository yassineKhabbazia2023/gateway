using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

public class CommercialProposalOrchestrationServiceTests
{
    private readonly Mock<IProspectApiClient> _prospectApiClient;
    private readonly Mock<IRegistryProspectClient> _registryProspectClient;
    private readonly CommercialProposalOrchestrationService _service;

    private const int ProspectId = 42;
    private const int CurrentUserId = 7;
    private const string AccountNumber = "AK-001";

    public CommercialProposalOrchestrationServiceTests()
    {
        _prospectApiClient = new Mock<IProspectApiClient>(MockBehavior.Strict);
        _registryProspectClient = new Mock<IRegistryProspectClient>(MockBehavior.Strict);
        _service = new CommercialProposalOrchestrationService(
            _prospectApiClient.Object,
            _registryProspectClient.Object);
    }

    private static Mock<IFormFile> BuildFileMock(string fileName = "proposal.pdf", string contentType = "application/pdf")
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(1024);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[1024]));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return file;
    }

    // ---- SendAsync ----

    [Fact]
    public async Task SendAsync_WhenProspectNotFound_ReturnsProspectNotFound()
    {
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommercialProposalEligibilityResponse?)null);

        var result = await _service.SendAsync(ProspectId, CurrentUserId, BuildFileMock().Object, CancellationToken.None);

        result.Should().Be(CommercialProposalOrchestrationOutcome.ProspectNotFound);
    }

    [Fact]
    public async Task SendAsync_WhenAlreadySent_ReturnsAlreadySent()
    {
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialProposalEligibilityResponse { CanSend = false, AlreadySent = true });

        var result = await _service.SendAsync(ProspectId, CurrentUserId, BuildFileMock().Object, CancellationToken.None);

        result.Should().Be(CommercialProposalOrchestrationOutcome.AlreadySent);
    }

    [Fact]
    public async Task SendAsync_WhenNotEligible_ReturnsNotEligible()
    {
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialProposalEligibilityResponse { CanSend = false, AlreadySent = false });

        var result = await _service.SendAsync(ProspectId, CurrentUserId, BuildFileMock().Object, CancellationToken.None);

        result.Should().Be(CommercialProposalOrchestrationOutcome.NotEligible);
    }

    [Fact]
    public async Task SendAsync_WhenAccountNumberNotFound_ReturnsAccountNumberNotFound()
    {
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialProposalEligibilityResponse { CanSend = true, AlreadySent = false });
        _prospectApiClient
            .Setup(c => c.GetAkuiteoAccountNumberByProspectIdAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.SendAsync(ProspectId, CurrentUserId, BuildFileMock().Object, CancellationToken.None);

        result.Should().Be(CommercialProposalOrchestrationOutcome.AccountNumberNotFound);
    }

    [Fact]
    public async Task SendAsync_WhenEligibleAndAccountFound_UploadsToRegistryThenCallsProspect()
    {
        var fileMock = BuildFileMock();
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialProposalEligibilityResponse { CanSend = true, AlreadySent = false });
        _prospectApiClient
            .Setup(c => c.GetAkuiteoAccountNumberByProspectIdAsync(ProspectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountNumber);
        _registryProspectClient
            .Setup(c => c.UploadAkuiteoDocumentAsync(AccountNumber, It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectApiClient
            .Setup(c => c.SendCommercialProposalAsync(ProspectId, CurrentUserId, fileMock.Object, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.SendAsync(ProspectId, CurrentUserId, fileMock.Object, CancellationToken.None);

        result.Should().Be(CommercialProposalOrchestrationOutcome.Sent);
        _registryProspectClient.Verify(
            c => c.UploadAkuiteoDocumentAsync(AccountNumber, It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _prospectApiClient.Verify(
            c => c.SendCommercialProposalAsync(ProspectId, CurrentUserId, fileMock.Object, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenNotEligible_DoesNotCallRegistry()
    {
        _prospectApiClient
            .Setup(c => c.GetCommercialProposalEligibilityAsync(ProspectId, CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercialProposalEligibilityResponse { CanSend = false, AlreadySent = false });

        await _service.SendAsync(ProspectId, CurrentUserId, BuildFileMock().Object, CancellationToken.None);

        _registryProspectClient.Verify(
            c => c.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
