using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="PaymentPreferencesOrchestrationService"/>.
/// </summary>
public sealed class PaymentPreferencesOrchestrationServiceTests
{
    private readonly Mock<IProspectApiClient> _prospectClient = new();
    private readonly Mock<IMandatePaymentPreferencesClient> _mandateClient = new();
    private readonly Mock<IRegistryProspectClient> _registryClient = new();
    private readonly Mock<IProspectService> _prospectService = new();
    private readonly Mock<IPaymentPreferenceNotificationService> _paymentPreferenceNotificationService = new();
    private readonly PaymentPreferencesOrchestrationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentPreferencesOrchestrationServiceTests"/> class.
    /// </summary>
    public PaymentPreferencesOrchestrationServiceTests()
    {
        _service = new PaymentPreferencesOrchestrationService(
            _prospectClient.Object,
            _mandateClient.Object,
            _registryClient.Object,
            _prospectService.Object,
            _paymentPreferenceNotificationService.Object,
            NullLogger<PaymentPreferencesOrchestrationService>.Instance);
    }

    /// <summary>
    /// Verifies that GET retrieves the prospect account before calling Mandat.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenProspectExists_ReturnsMandatPaymentPreference()
    {
        var expected = new MandatePaymentPreferenceResponse { PaymentType = "OTHER" };
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("OTHER");
        _mandateClient.Verify(client => client.GetAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that signed mandate download resolves the Prospect document identifier through Mandat.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenSignedDocumentExists_ReturnsProspectDocument()
    {
        var expected = new ProspectDocumentContentResponse([4, 5, 6], "application/pdf", "mandat-AK-001-signature.pdf");
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("456");
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.DownloadSignedSepaMandateAsync(10, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Verifies that signed mandate download returns null when the prospect cannot be resolved.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenProspectDoesNotExist_ReturnsNull()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectAccountResponse?)null);

        var result = await _service.DownloadSignedSepaMandateAsync(10, CancellationToken.None);

        result.Should().BeNull();
        _mandateClient.Verify(client => client.GetSignedMandateDocumentIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that signed mandate download returns null when Mandat has no signed document identifier.
    /// </summary>
    [Fact]
    public async Task DownloadSignedSepaMandateAsync_WhenMandatHasNoSignedDocumentId_ReturnsNull()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.DownloadSignedSepaMandateAsync(10, CancellationToken.None);

        result.Should().BeNull();
        _prospectClient.Verify(client => client.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that GET uploads the RIB and signed mandate before marking Mandat when synchronization returned a signed PDF.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenMandatReturnsSignedMandate_UploadsDocumentsAndMarksSent()
    {
        var calls = new List<string>();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                SignedMandateContentType = "application/pdf",
                SignedMandateFileName = "signed.pdf",
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP"
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "rib.pdf"),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("rib-akuiteo"))
            .ReturnsAsync(true);
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "mandat-AK-001-signature.pdf"),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("signed-mandate-akuiteo"))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(
                10,
                0,
                "user@test.fr",
                "SIGNED_MANDATE",
                It.Is<IFormFile>(file =>
                    file.FileName == "mandat-AK-001-signature.pdf"
                    && file.ContentType == "application/pdf"),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("signed-mandate-prospect"))
            .ReturnsAsync(456);
        _mandateClient
            .Setup(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"))
            .Callback(() => calls.Add("save-document-id"))
            .ReturnsAsync(true);
        _mandateClient
            .Setup(client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("extract-bank-details"))
            .ReturnsAsync(CreateExtractedBankDetails());
        _registryClient
            .Setup(client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.Is<AkuiteoBankingInformationRequest>(candidate =>
                    candidate.Action == "ADD"
                    && candidate.Sepa.BankDetails.Entity == "30006"
                    && candidate.Sepa.BankDetails.Counter == "00001"
                    && candidate.Sepa.BankDetails.AccountNumber == "12345678901"
                    && candidate.Sepa.BankDetails.Key == "89"
                    && candidate.Sepa.BankDetails.Domiciliation == "AGRI"
                    && candidate.Sepa.Bic.Country == "FR"
                    && candidate.Sepa.Bic.Bank == "AGRI"
                    && candidate.Sepa.Bic.Location == "FR"
                    && candidate.Sepa.Bic.Branch == "PP"
                    && candidate.Sepa.Iban.Country == "FR"
                    && candidate.Sepa.Iban.Key == "76"
                    && candidate.Sepa.Iban.AccountNumber == "30006000011234567890189"),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("update-banking-information"))
            .ReturnsAsync(true);
        _registryClient
            .Setup(client => client.PatchAkuiteoAccountPaymentMethodAsync(
                42,
                It.Is<AkuiteoAccountPaymentMethodRequest>(candidate =>
                    candidate.ConditionOfPayment.DeadLine == string.Empty
                    && candidate.ConditionOfPayment.Term == string.Empty
                    && candidate.ConditionOfPayment.Day == 0
                    && candidate.MethodOfPayment == "DIRECT_DEBIT"),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("patch-payment-method"))
            .ReturnsAsync(true);
        _mandateClient
            .Setup(client => client.MarkSentToAkuiteoAsync(42, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("mark-sent"))
            .ReturnsAsync(true);
        _prospectService
            .Setup(service => service.CompleteStepAsync(
                42,
                It.Is<CompleteStepRequest>(req => req.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                0))
            .Callback(() => calls.Add("complete-step"))
            .ReturnsAsync(new DocumentUploadResultResponse([], []));

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _prospectClient.Verify(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()), Times.Once);
        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "mandat-AK-001-signature.pdf"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _prospectClient.Verify(
            client => client.UploadDocumentAsync(
                10,
                0,
                "user@test.fr",
                "SIGNED_MANDATE",
                It.Is<IFormFile>(file => file.FileName == "mandat-AK-001-signature.pdf"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mandateClient.Verify(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"), Times.Once);
        _mandateClient.Verify(
            client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()),
            Times.Once);
        _registryClient.Verify(
            client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _registryClient.Verify(
            client => client.PatchAkuiteoAccountPaymentMethodAsync(
                42,
                It.IsAny<AkuiteoAccountPaymentMethodRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mandateClient.Verify(client => client.MarkSentToAkuiteoAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                42,
                It.Is<CompleteStepRequest>(req => req.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                0),
            Times.Once);
        calls.Should().Equal(
            "rib-akuiteo",
            "signed-mandate-prospect",
            "save-document-id",
            "signed-mandate-akuiteo",
            "extract-bank-details",
            "update-banking-information",
            "patch-payment-method",
            "mark-sent",
            "complete-step");
    }

    /// <summary>
    /// Verifies that signed-mandate finalization stops when Mandat omits the persisted bank identifiers.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenSignedMandateBankIdentifiersAreMissing_DoesNotStartFinalization()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
            });

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _prospectClient.Verify(
            client => client.GetDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.ExtractBankDetailsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that GET does not mark Mandat when one Akuiteo upload fails.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenAkuiteoUploadFails_DoesNotMarkSent()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _prospectClient.Verify(
            client => client.UploadDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SaveSignedMandateDocumentIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
            Times.Never);
        _mandateClient.Verify(client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that GET does not upload the signed mandate to Akuiteo when Prospect does not persist it.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenProspectSignedMandateUploadFails_DoesNotUploadSignedMandateToAkuiteoOrMarkSent()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(
                10,
                0,
                "user@test.fr",
                "SIGNED_MANDATE",
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _registryClient.Verify(
            client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "mandat-AK-001-signature.pdf"),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SaveSignedMandateDocumentIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
            Times.Never);
        _mandateClient.Verify(client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that GET saves the signed mandate document identifier when Prospect persisted it, even if the signed mandate Akuiteo upload fails.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenSignedMandateAkuiteoUploadFails_SavesDocumentIdButDoesNotMarkSent()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "rib.pdf"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(
                10,
                0,
                "user@test.fr",
                "SIGNED_MANDATE",
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(456);
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "mandat-AK-001-signature.pdf"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mandateClient
            .Setup(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"))
            .ReturnsAsync(true);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _mandateClient.Verify(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"), Times.Once);
        _mandateClient.Verify(client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that GET reuses an already persisted signed mandate document identifier on retry.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenSignedMandateDocumentIdAlreadyExists_DoesNotUploadSignedMandateToProspectAgain()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandateDocumentId = "456",
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                SignedMandateContentType = "application/pdf",
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mandateClient
            .Setup(client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExtractedBankDetails());
        _registryClient
            .Setup(client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _registryClient
            .Setup(client => client.PatchAkuiteoAccountPaymentMethodAsync(
                42,
                It.IsAny<AkuiteoAccountPaymentMethodRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mandateClient
            .Setup(client => client.MarkSentToAkuiteoAsync(
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _prospectClient.Verify(
            client => client.UploadDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SaveSignedMandateDocumentIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.MarkSentToAkuiteoAsync(
                42,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GET does not mark Mandat when saving the signed mandate document identifier fails.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenSignedMandateDocumentIdSaveFails_DoesNotMarkSent()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(
                10,
                0,
                "user@test.fr",
                "SIGNED_MANDATE",
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(456);
        _mandateClient
            .Setup(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"))
            .ReturnsAsync(false);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _mandateClient.Verify(client => client.SaveSignedMandateDocumentIdAsync(42, It.IsAny<CancellationToken>(), "456"), Times.Once);
        _mandateClient.Verify(client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

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
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42, Email = "signatory@test.fr" });
        _mandateClient
            .Setup(client => client.SetOtherAsync(42, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectService
            .Setup(service => service.CompleteStepAsync(
                42,
                It.Is<CompleteStepRequest>(request => request.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                7))
            .ReturnsAsync(new DocumentUploadResultResponse([], []));
        _paymentPreferenceNotificationService
            .Setup(service => service.GetCollabEmailReceivers())
            .Returns(["bs@test.fr"]);

        var result = await _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        result.Should().BeTrue();
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                42,
                It.Is<CompleteStepRequest>(request => request.StepName == "PAYMENT_METHOD"),
                It.IsAny<CancellationToken>(),
                7),
            Times.Once);
        _paymentPreferenceNotificationService.Verify(
            service => service.SendAsync(
                10,
                "signatory@test.fr",
                It.Is<string[]>(receivers => receivers.SequenceEqual(new[] { "bs@test.fr" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that POST OTHER passes the signatory and collaborator receivers to the notification service.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenCompletionSucceeds_PassesNotificationTargets()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42, Email = "signatory@test.fr" });
        _mandateClient
            .Setup(client => client.SetOtherAsync(42, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectService
            .Setup(service => service.CompleteStepAsync(
                42,
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                7))
            .ReturnsAsync(new DocumentUploadResultResponse([], []));
        _paymentPreferenceNotificationService
            .Setup(service => service.GetCollabEmailReceivers())
            .Returns(["bs1@test.fr", "bs2@test.fr"]);

        var result = await _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        result.Should().BeTrue();
        _paymentPreferenceNotificationService.Verify(
            service => service.SendAsync(
                10,
                "signatory@test.fr",
                It.Is<string[]>(receivers => receivers.SequenceEqual(new[] { "bs1@test.fr", "bs2@test.fr" })),
                It.IsAny<CancellationToken>()),
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
        _paymentPreferenceNotificationService.Verify(
            service => service.SendAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST OTHER does not notify when completion fails.
    /// </summary>
    [Fact]
    public async Task SetOtherAsync_WhenCompletionFails_DoesNotNotify()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42, Email = "signatory@test.fr" });
        _mandateClient
            .Setup(client => client.SetOtherAsync(42, "user@test.fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectService
            .Setup(service => service.CompleteStepAsync(
                42,
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                7))
            .ThrowsAsync(new InvalidOperationException("completion failed"));

        Func<Task> act = () => _service.SetOtherAsync(10, "user@test.fr", 7, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _paymentPreferenceNotificationService.Verify(
            service => service.SendAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()),
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
    /// Verifies that POST SEPA calls Prospect, Mandat, then marks payment method in progress.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedUserIsSignatory_ReturnsSignatureUrlAndMarksStepInProgress()
    {
        var request = CreateSepaRequest();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse
            {
                ProspectId = 10,
                AccountId = 42,
                Email = "first.signatory@test.fr",
                FirstName = "Prospect",
                LastName = "Signatory"
            });
        _prospectClient
            .Setup(client => client.IsProspectSignatoryAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(10, 7, "user@test.fr", "RIB", request.File!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(123);
        _mandateClient
            .Setup(client => client.SetSepaAsync(
                42,
                It.Is<MandateSepaPaymentPreferenceRequest>(candidate =>
                    candidate.DocumentId == 123
                    && candidate.AccountHolder == request.AccountHolder
                    && candidate.Address == request.Address
                    && candidate.AddressLine2 == request.AddressLine2
                    && candidate.City == request.City
                    && candidate.Country == request.Country
                    && candidate.PostalCode == request.PostalCode
                    && candidate.Iban == request.Iban
                    && candidate.Bic == request.Bic
                    && candidate.RecipientEmail == "user@test.fr"
                    && candidate.RecipientFirstName == "Connected"
                    && candidate.RecipientLastName == "User"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://signature.test");
        _prospectClient
            .Setup(client => client.MarkPaymentMethodInProgressAsync(10, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.SetSepaAsync(
            10,
            request,
            "user@test.fr",
            "Connected",
            "User",
            7,
            CancellationToken.None);

        result.Outcome.Should().Be(SepaPaymentPreferenceOrchestrationOutcome.Completed);
        result.SignatureUrl.Should().Be("https://signature.test");
        _prospectClient.Verify(
            client => client.GetDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospectClient.Verify(client => client.MarkPaymentMethodInProgressAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        _mandateClient.Verify(
            client => client.ExtractBankDetailsAsync(request.Iban, request.Bic, It.IsAny<CancellationToken>()),
            Times.Never);
        _registryClient.Verify(
            client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _registryClient.Verify(
            client => client.PatchAkuiteoAccountPaymentMethodAsync(
                42,
                It.IsAny<AkuiteoAccountPaymentMethodRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that signed-mandate finalization stops before Registry when Mandat rejects bank-details extraction.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenBankDetailsExtractionFails_DoesNotMarkMandateSent()
    {
        SetupSignedMandateReadyForAccountUpdate();
        _mandateClient
            .Setup(client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MandateBankDetailsExtractionResponse?)null);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _registryClient.Verify(
            client => client.UpdateAkuiteoBankingInformationAsync(
                It.IsAny<int>(),
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that signed-mandate finalization does not patch the payment method when the banking-information update fails.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenBankingInformationUpdateFails_DoesNotPatchPaymentMethod()
    {
        SetupSignedMandateReadyForAccountUpdate();
        _mandateClient
            .Setup(client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExtractedBankDetails());
        _registryClient
            .Setup(client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _registryClient.Verify(
            client => client.PatchAkuiteoAccountPaymentMethodAsync(
                It.IsAny<int>(),
                It.IsAny<AkuiteoAccountPaymentMethodRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that signed-mandate finalization does not mark the mandate sent when the direct-debit patch fails.
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenPaymentMethodPatchFails_DoesNotMarkMandateSent()
    {
        SetupSignedMandateReadyForAccountUpdate();
        _mandateClient
            .Setup(client => client.ExtractBankDetailsAsync(
                "FR7630006000011234567890189",
                "AGRIFRPP",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExtractedBankDetails());
        _registryClient
            .Setup(client => client.UpdateAkuiteoBankingInformationAsync(
                42,
                It.IsAny<AkuiteoBankingInformationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _registryClient
            .Setup(client => client.PatchAkuiteoAccountPaymentMethodAsync(
                42,
                It.IsAny<AkuiteoAccountPaymentMethodRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetAsync(10, "user@test.fr", CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentType.Should().Be("MANDATE_SEPA");
        _mandateClient.Verify(
            client => client.MarkSentToAkuiteoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _prospectService.Verify(
            service => service.CompleteStepAsync(
                It.IsAny<int>(),
                It.IsAny<CompleteStepRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA rejects connected users who are not the prospect signatory before any side effect.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenConnectedUserIsNotSignatory_DoesNotUploadOrCallMandat()
    {
        var request = CreateSepaRequest();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse
            {
                ProspectId = 10,
                AccountId = 42,
                Email = "first.signatory@test.fr"
            });
        _prospectClient
            .Setup(client => client.IsProspectSignatoryAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.SetSepaAsync(
            10,
            request,
            "user@test.fr",
            "Connected",
            "User",
            7,
            CancellationToken.None);

        result.Outcome.Should().Be(SepaPaymentPreferenceOrchestrationOutcome.Forbidden);
        _prospectClient.Verify(
            client => client.IsProspectSignatoryAsync(10, 7, It.IsAny<CancellationToken>()),
            Times.Once);
        _prospectClient.Verify(
            client => client.UploadDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<MandateSepaPaymentPreferenceRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospectClient.Verify(
            client => client.MarkPaymentMethodInProgressAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA stops before signatory checks when the prospect cannot be resolved.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenProspectDoesNotExist_ReturnsNotFoundWithoutSideEffects()
    {
        var request = CreateSepaRequest();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectAccountResponse?)null);

        var result = await _service.SetSepaAsync(
            10,
            request,
            "user@test.fr",
            "Connected",
            "User",
            7,
            CancellationToken.None);

        result.Outcome.Should().Be(SepaPaymentPreferenceOrchestrationOutcome.NotFound);
        _prospectClient.Verify(
            client => client.IsProspectSignatoryAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospectClient.Verify(
            client => client.UploadDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<MandateSepaPaymentPreferenceRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA stops when RIB upload fails.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenRibUploadFails_ReturnsNotFoundWithoutMandatCall()
    {
        var request = CreateSepaRequest();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _prospectClient
            .Setup(client => client.IsProspectSignatoryAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(10, 7, "user@test.fr", "RIB", request.File!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _service.SetSepaAsync(
            10,
            request,
            "user@test.fr",
            "Connected",
            "User",
            7,
            CancellationToken.None);

        result.Outcome.Should().Be(SepaPaymentPreferenceOrchestrationOutcome.NotFound);
        _prospectClient.Verify(
            client => client.GetDocumentAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _mandateClient.Verify(
            client => client.SetSepaAsync(
                It.IsAny<int>(),
                It.IsAny<MandateSepaPaymentPreferenceRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospectClient.Verify(
            client => client.MarkPaymentMethodInProgressAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that POST SEPA does not mark the step in progress when Mandat fails.
    /// </summary>
    [Fact]
    public async Task SetSepaAsync_WhenMandatFails_DoesNotMarkStepInProgress()
    {
        var request = CreateSepaRequest();
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse
            {
                ProspectId = 10,
                AccountId = 42,
                Email = "user@test.fr"
            });
        _prospectClient
            .Setup(client => client.IsProspectSignatoryAsync(10, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospectClient
            .Setup(client => client.UploadDocumentAsync(10, 7, "user@test.fr", "RIB", request.File!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(123);
        _mandateClient
            .Setup(client => client.SetSepaAsync(42, It.IsAny<MandateSepaPaymentPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.SetSepaAsync(
            10,
            request,
            "user@test.fr",
            "Connected",
            "User",
            7,
            CancellationToken.None);

        result.Outcome.Should().Be(SepaPaymentPreferenceOrchestrationOutcome.MandateFailed);
        _prospectClient.Verify(
            client => client.MarkPaymentMethodInProgressAsync(
                It.IsAny<int>(),
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
    /// Configures a signed mandate whose documents are ready for the Akuiteo account updates.
    /// </summary>
    private void SetupSignedMandateReadyForAccountUpdate()
    {
        _prospectClient
            .Setup(client => client.GetProspectAccountAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectAccountResponse { ProspectId = 10, AccountId = 42 });
        _mandateClient
            .Setup(client => client.GetAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MandatePaymentPreferenceResponse
            {
                PaymentType = "MANDATE_SEPA",
                AccountId = 42,
                RibDocumentId = 123,
                SignedMandateDocumentId = "456",
                SignedMandatePdfBase64 = Convert.ToBase64String([4, 5, 6]),
                SignedMandateContentType = "application/pdf",
                Iban = "FR7630006000011234567890189",
                Bic = "AGRIFRPP",
            });
        _prospectClient
            .Setup(client => client.GetDocumentAsync(10, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "rib.pdf"));
        _prospectClient
            .Setup(client => client.GetAkuiteoAccountNumberByProspectIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync("AK-001");
        _registryClient
            .Setup(client => client.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.IsAny<ProspectDocumentContentResponse>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Creates a complete Mandat bank-details extraction response.
    /// </summary>
    /// <returns>The extracted banking details.</returns>
    private static MandateBankDetailsExtractionResponse CreateExtractedBankDetails()
    {
        return new MandateBankDetailsExtractionResponse
        {
            Iban = new MandateExtractedIbanResponse
            {
                CountryCode = "FR",
                CheckDigits = "76",
                BankAccountPart = "30006000011234567890189"
            },
            Rib = new MandateExtractedRibResponse
            {
                BankCode = "30006",
                BranchCode = "00001",
                AccountNumber = "12345678901",
                RibKey = "89"
            },
            Bic = new MandateExtractedBicResponse
            {
                CountryCode = "FR",
                BankCode = "AGRI",
                LocationCode = "FR",
                BranchCode = "PP"
            },
            Domiciliation = "AGRI"
        };
    }

    /// <summary>
    /// Creates a valid SEPA request.
    /// </summary>
    /// <returns>The request.</returns>
    private static SepaPaymentPreferenceRequest CreateSepaRequest()
    {
        var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "rib.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        return new SepaPaymentPreferenceRequest
        {
            File = file,
            AccountHolder = "Jean Dupont",
            Address = "10 rue de Paris",
            AddressLine2 = "Batiment A",
            City = "Paris",
            Country = "France",
            PostalCode = "75008",
            Iban = "FR7630006000011234567890189",
            Bic = "AGRIFRPP"
        };
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

