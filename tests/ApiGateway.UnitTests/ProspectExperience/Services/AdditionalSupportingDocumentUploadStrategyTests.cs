using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="AdditionalSupportingDocumentUploadStrategy"/>.
/// </summary>
public sealed class AdditionalSupportingDocumentUploadStrategyTests
{
    private readonly Mock<IRegistryProspectClient> _registryClient;
    private readonly Mock<IProspectApiClient> _prospectClient;
    private readonly AdditionalSupportingDocumentUploadStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdditionalSupportingDocumentUploadStrategyTests"/> class.
    /// </summary>
    public AdditionalSupportingDocumentUploadStrategyTests()
    {
        _registryClient = new Mock<IRegistryProspectClient>();
        _prospectClient = new Mock<IProspectApiClient>();
        _strategy = new AdditionalSupportingDocumentUploadStrategy(
            _registryClient.Object,
            _prospectClient.Object,
            Mock.Of<ILogger<AdditionalSupportingDocumentUploadStrategy>>());
    }

    /// <summary>
    /// Verifies that a complementary supporting document is uploaded to Akuitéo and marked as uploaded.
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenAkuiteoUploadSucceeds_PersistsSuccess()
    {
        var document = new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "autre.pdf");
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", document, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.StepName == "SupportingDocuments"
                    && request.SucceededDocumentIds.SequenceEqual(new[] { 99 })
                    && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([99], []));

        var result = await _strategy.UploadAsync(42, 99, CancellationToken.None);

        result.SucceededDocumentIds.Should().Equal(99);
        result.FailedDocumentIds.Should().BeEmpty();
        _prospectClient.Verify(
            client => client.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that a failed Akuitéo upload is persisted as a failed document result.
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenAkuiteoUploadFails_PersistsFailure()
    {
        var document = new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "autre.pdf");
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", document, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.StepName == "SupportingDocuments"
                    && request.SucceededDocumentIds.Count == 0
                    && request.FailedDocumentIds.SequenceEqual(new[] { 99 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([], [99]));

        var result = await _strategy.UploadAsync(42, 99, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().Equal(99);
    }

    /// <summary>
    /// Verifies that a missing Akuitéo account number prevents direct complementary document upload.
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenAccountNumberIsMissing_ThrowsGatewayException()
    {
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        Func<Task> act = () => _strategy.UploadAsync(42, 99, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _prospectClient.Verify(
            client => client.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
