using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Dynamic;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="SupportingDocumentsStepCompletionStrategy"/>.
/// </summary>
public sealed class SupportingDocumentsStepCompletionStrategyTests
{
    private readonly Mock<IRegistryProspectClient> _registryClient = new();
    private readonly Mock<IProspectApiClient> _prospectClient = new();
    private readonly SupportingDocumentsStepCompletionStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportingDocumentsStepCompletionStrategyTests"/> class.
    /// </summary>
    public SupportingDocumentsStepCompletionStrategyTests()
    {
        _strategy = new SupportingDocumentsStepCompletionStrategy(
            _registryClient.Object,
            _prospectClient.Object,
            Mock.Of<ILogger<SupportingDocumentsStepCompletionStrategy>>());
    }

    /// <summary>
    /// Verifies that the strategy handles the SupportingDocuments step.
    /// </summary>
    [Fact]
    public void CanHandle_SupportingDocumentsStep_ReturnsTrue()
    {
        _strategy.CanHandle("SupportingDocuments").Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the strategy handles the SupportingDocuments step case-insensitively.
    /// </summary>
    /// <param name="stepName">The step name with different casing.</param>
    [Theory]
    [InlineData("supportingdocuments")]
    [InlineData("SUPPORTINGDOCUMENTS")]
    [InlineData("SupportingDocuments")]
    [InlineData("supportingDocuments")]
    public void CanHandle_CaseInsensitive_ReturnsTrue(string stepName)
    {
        _strategy.CanHandle(stepName).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the strategy does not handle other step names.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    [Theory]
    [InlineData("LegalFormData")]
    [InlineData("Beneficiaries")]
    [InlineData("BeneficiaryDocuments")]
    [InlineData("")]
    [InlineData(" ")]
    public void CanHandle_OtherStepName_ReturnsFalse(string stepName)
    {
        _strategy.CanHandle(stepName).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a null upload plan throws GatewayException with 404 status.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_UploadPlanNull_ThrowsGatewayException404()
    {
        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentsToUploadToExternalServiceResponse?)null);

        Func<Task> act = () => _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.ErrorCode.Should().Be(Errors.NullArgumentCode);
        exception.Which.Message.Should().Be("Prospect or onboarding step was not found.");

        _prospectClient.Verify(
            client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when there are no documents to upload, the strategy returns an empty result without completing the step.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_NoDocumentsToUpload_ReturnsEmptyResult()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.StepAlreadyCompleted);

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();

        _prospectClient.Verify(
            client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _prospectClient.Verify(
            client => client.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when Akuiteo account number is missing, the strategy throws GatewayException with 400 status.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AkuiteoAccountNumberMissing_ThrowsGatewayException400()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            null,
            new List<int> { 1, 2, 3 },
            DocumentsToUploadToExternalServiceStatus.PendingDocuments);

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        Func<Task> act = () => _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.ErrorCode.Should().Be(Errors.NullArgumentCode);
        exception.Which.Message.Should().Be("The prospect Akuitéo account number is required to upload onboarding step documents.");

        _prospectClient.Verify(
            client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when all documents are uploaded successfully, the step is completed.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AllDocumentsUploadedSuccessfully_CompletesStep()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            new List<int> { 1, 2, 3 },
            DocumentsToUploadToExternalServiceStatus.PendingDocuments);

        var document1 = new ProspectDocumentContentResponse(
            new byte[] { 0x01, 0x02 },
            "application/pdf",
            "document1.pdf");

        var document2 = new ProspectDocumentContentResponse(
            new byte[] { 0x03, 0x04 },
            "application/pdf",
            "document2.pdf");

        var document3 = new ProspectDocumentContentResponse(
            new byte[] { 0x05, 0x06 },
            "application/pdf",
            "document3.pdf");

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document1);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document2);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document3);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.IsAny<DocumentUploadResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse(Array.Empty<int>(), Array.Empty<int>()));

        _prospectClient
            .Setup(client => client.CompleteStepAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().HaveCount(3);
        result.SucceededDocumentIds.Should().Contain(new[] { 1, 2, 3 });
        result.FailedDocumentIds.Should().BeEmpty();

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 3, It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document1,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document2,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document3,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(r =>
                    r.StepName == "SupportingDocuments" &&
                    r.SucceededDocumentIds.Count == 1 &&
                    r.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _prospectClient.Verify(
            client => client.CompleteStepAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when some documents fail to upload, the step is not completed and failures are tracked.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_PartialFailure_DoesNotCompleteStep_TracksFailures()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            new List<int> { 1, 2, 3 },
            DocumentsToUploadToExternalServiceStatus.PendingDocuments);

        var document1 = new ProspectDocumentContentResponse(
            new byte[] { 0x01, 0x02 },
            "application/pdf",
            "document1.pdf");

        var document2 = new ProspectDocumentContentResponse(
            new byte[] { 0x03, 0x04 },
            "application/pdf",
            "document2.pdf");

        var document3 = new ProspectDocumentContentResponse(
            new byte[] { 0x05, 0x06 },
            "application/pdf",
            "document3.pdf");

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document1);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document2);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document3);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.IsAny<DocumentUploadResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse(Array.Empty<int>(), Array.Empty<int>()));

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().HaveCount(2);
        result.SucceededDocumentIds.Should().Contain(new[] { 1, 3 });
        result.FailedDocumentIds.Should().HaveCount(1);
        result.FailedDocumentIds.Should().Contain(2);

        _prospectClient.Verify(
            client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(r =>
                    r.StepName == "SupportingDocuments" &&
                    r.SucceededDocumentIds.Count == 1 &&
                    r.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _prospectClient.Verify(
            client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(r =>
                    r.StepName == "SupportingDocuments" &&
                    r.SucceededDocumentIds.Count == 0 &&
                    r.FailedDocumentIds.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when a document download fails, it is tracked as failed and processing continues for other documents.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_DocumentDownloadFails_TracksAsFailedContinuesOthers()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            new List<int> { 1, 2, 3 },
            DocumentsToUploadToExternalServiceStatus.PendingDocuments);

        var document1 = new ProspectDocumentContentResponse(
            new byte[] { 0x01, 0x02 },
            "application/pdf",
            "document1.pdf");

        var document3 = new ProspectDocumentContentResponse(
            new byte[] { 0x05, 0x06 },
            "application/pdf",
            "document3.pdf");

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document1);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectDocumentContentResponse?)null);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document3);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.IsAny<DocumentUploadResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse(Array.Empty<int>(), Array.Empty<int>()));

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().HaveCount(2);
        result.SucceededDocumentIds.Should().Contain(new[] { 1, 3 });
        result.FailedDocumentIds.Should().HaveCount(1);
        result.FailedDocumentIds.Should().Contain(2);

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.GetDocumentAsync(42, 3, It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document1,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document3,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _prospectClient.Verify(
            client => client.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when Registry upload fails for a document, it is tracked as failed and partial results are persisted.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_RegistryUploadFails_TracksAsFailedPersistsPartialResults()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            new List<int> { 1, 2 },
            DocumentsToUploadToExternalServiceStatus.PendingDocuments);

        var document1 = new ProspectDocumentContentResponse(
            new byte[] { 0x01, 0x02 },
            "application/pdf",
            "document1.pdf");

        var document2 = new ProspectDocumentContentResponse(
            new byte[] { 0x03, 0x04 },
            "application/pdf",
            "document2.pdf");

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document1);

        _prospectClient
            .Setup(client => client.GetDocumentAsync(42, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document2);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK12345",
                document2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _prospectClient
            .Setup(client => client.RegisterDocumentUploadResultAsync(
                42,
                It.IsAny<DocumentUploadResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse(Array.Empty<int>(), Array.Empty<int>()));

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().HaveCount(1);
        result.SucceededDocumentIds.Should().Contain(1);
        result.FailedDocumentIds.Should().HaveCount(1);
        result.FailedDocumentIds.Should().Contain(2);

        _prospectClient.Verify(
            client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(r =>
                    r.StepName == "SupportingDocuments" &&
                    r.SucceededDocumentIds.Contains(1) &&
                    r.SucceededDocumentIds.Count == 1 &&
                    r.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(r =>
                    r.StepName == "SupportingDocuments" &&
                    r.SucceededDocumentIds.Count == 0 &&
                    r.FailedDocumentIds.Contains(2) &&
                    r.FailedDocumentIds.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _prospectClient.Verify(
            client => client.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when required documents are missing per legal form, the strategy throws GatewayException with 422 status.
    /// This prevents completion when required supporting documents (e.g., KBIS for SARL) have not been uploaded.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_MissingRequiredDocuments_ThrowsGatewayException422()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        var requirements = new DocumentRequirementsResponse
        {
            Documents = new List<RequiredDocumentItem>
            {
                new() { Type = "KBIS", MaxFiles = 1, Documents = [] }, // Missing!
                new() { Type = "STATUTS", MaxFiles = 1, Documents = [new ExpandoObject()] } // Uploaded
            }
        };

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        Func<Task> act = () => _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        exception.Which.ErrorCode.Should().Be("SupportingDocumentsStepNotReady");
        exception.Which.Message.Should().Contain("KBIS");

        _prospectClient.Verify(
            client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);

        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when all required documents are uploaded, completion succeeds.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AllRequiredDocumentsUploaded_Succeeds()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        var requirements = new DocumentRequirementsResponse
        {
            Documents = new List<RequiredDocumentItem>
            {
                new() { Type = "KBIS", MaxFiles = 1, Documents = [new ExpandoObject()] }, // Uploaded
                new() { Type = "STATUTS", MaxFiles = 1, Documents = [new ExpandoObject()] } // Uploaded
            }
        };

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();

        _prospectClient.Verify(
            client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that "AUTRES" document type is optional and does not block completion even when missing.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AutresTypeMissing_AllowsCompletion()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        var requirements = new DocumentRequirementsResponse
        {
            Documents = new List<RequiredDocumentItem>
            {
                new() { Type = "KBIS", MaxFiles = 1, Documents = [new ExpandoObject()] }, // Uploaded
                new() { Type = "AUTRES", MaxFiles = 1, Documents = [] } // Missing but optional!
            }
        };

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();

        _prospectClient.Verify(
            client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that "AUTRES" type check is case-insensitive.
    /// </summary>
    [Theory]
    [InlineData("autres")]
    [InlineData("Autres")]
    [InlineData("AUTRES")]
    [InlineData("AuTrEs")]
    public async Task CompleteAsync_AutresTypeCaseInsensitive_AllowsCompletion(string autresVariant)
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        var requirements = new DocumentRequirementsResponse
        {
            Documents = new List<RequiredDocumentItem>
            {
                new() { Type = "KBIS", MaxFiles = 1, Documents = [new ExpandoObject()] }, // Uploaded
                new() { Type = autresVariant, MaxFiles = 1, Documents = [] } // Missing but optional (any casing)
            }
        };

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that when "AUTRES" is missing along with other required documents,
    /// only the non-AUTRES types are reported as missing.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AutresAndOtherDocsMissing_ThrowsWithOnlyNonAutresTypes()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        var requirements = new DocumentRequirementsResponse
        {
            Documents = new List<RequiredDocumentItem>
            {
                new() { Type = "KBIS", MaxFiles = 1, Documents = [] }, // Missing - REQUIRED
                new() { Type = "AUTRES", MaxFiles = 1, Documents = [] }, // Missing - OPTIONAL
                new() { Type = "STATUTS", MaxFiles = 1, Documents = [new ExpandoObject()] } // Uploaded
            }
        };

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        Func<Task> act = () => _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        exception.Which.ErrorCode.Should().Be("SupportingDocumentsStepNotReady");
        exception.Which.Message.Should().Contain("KBIS");
        exception.Which.Message.Should().NotContain("AUTRES"); // AUTRES should NOT be in error message
    }

    /// <summary>
    /// Verifies graceful fallback when GetDocumentRequirementsAsync returns null.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_RequirementsNull_AllowsCompletion()
    {
        var uploadPlan = new DocumentsToUploadToExternalServiceResponse(
            "AK12345",
            Array.Empty<int>(),
            DocumentsToUploadToExternalServiceStatus.NoDocumentsToUpload);

        _prospectClient
            .Setup(client => client.GetDocumentsToUploadToExternalServiceAsync(
                42,
                "SupportingDocuments",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadPlan);

        _prospectClient
            .Setup(client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentRequirementsResponse?)null);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();

        _prospectClient.Verify(
            client => client.GetDocumentRequirementsAsync(42, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
