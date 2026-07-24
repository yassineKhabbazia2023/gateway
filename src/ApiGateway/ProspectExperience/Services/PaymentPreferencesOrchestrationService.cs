using ApiGateway.ProspectExperience.Helpers;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Responses;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace ApiGateway.ProspectExperience.Services;

/// <inheritdoc />
public sealed class PaymentPreferencesOrchestrationService(
    IProspectApiClient prospectClient,
    IMandatePaymentPreferencesClient mandateClient,
    IRegistryProspectClient registryClient,
    IProspectService prospectService,
    IPaymentPreferenceNotificationService paymentPreferenceNotificationService,
    ILogger<PaymentPreferencesOrchestrationService> logger) : IPaymentPreferencesOrchestrationService
{
    private const int SystemUserId = 0;
    private const string PaymentMethodStepName = "PAYMENT_METHOD";
    private const string RibDocumentType = "RIB";
    private const string SignedMandateDocumentType = "SIGNED_MANDATE";
    private const string PdfContentType = "application/pdf";

    /// <inheritdoc />
    public async Task<PaymentPreferenceResponse?> GetAsync(int prospectId, string? contactEmail, CancellationToken ct)
    {
        logger.LogInformation("Reading payment preference for prospect {ProspectId}", prospectId);

        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Payment preference requested for unknown prospect {ProspectId}", prospectId);
            return null;
        }

        logger.LogInformation(
            "Resolved prospect {ProspectId} to account {AccountId} before reading payment preference",
            prospectId,
            prospect.AccountId);

        var preference = await mandateClient.GetAsync(prospect.AccountId, ct);
        if (preference is null)
        {
            logger.LogInformation(
                "No payment preference was returned for prospect {ProspectId} and account {AccountId}",
                prospectId,
                prospect.AccountId);
            return null;
        }

        logger.LogInformation(
            "Mandat returned payment preference {PaymentType} for prospect {ProspectId} and account {AccountId}",
            preference.PaymentType,
            prospectId,
            prospect.AccountId);

        if (preference.HasSignedMandate)
        {
            await FinalizeSignedSepaAsync(prospectId, preference, contactEmail, ct);
        }

        return new PaymentPreferenceResponse { PaymentType = preference.PaymentType };
    }

    /// <inheritdoc />
    public async Task<ProspectDocumentContentResponse?> DownloadSignedSepaMandateAsync(int prospectId, CancellationToken ct)
    {
        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Signed SEPA mandate download requested for unknown prospect {ProspectId}", prospectId);
            return null;
        }

        var signedMandateDocumentId = await mandateClient.GetSignedMandateDocumentIdAsync(prospect.AccountId, ct);
        if (!int.TryParse(signedMandateDocumentId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var documentId))
        {
            logger.LogWarning(
                "Signed SEPA mandate download requested for prospect {ProspectId}, but Mandat did not return a valid Prospect document id",
                prospectId);
            return null;
        }

        return await prospectClient.GetDocumentAsync(prospectId, documentId, ct);
    }

    /// <summary>
    /// Finalizes a signed SEPA mandate after Mandat reports the signature.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="preference">The internal Mandat payment preference response.</param>
    /// <param name="contactEmail">The authenticated user email used for document audit headers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task FinalizeSignedSepaAsync(
        int prospectId,
        MandatePaymentPreferenceResponse preference,
        string? contactEmail,
        CancellationToken ct)
    {
        var accountId = preference.AccountId!.Value;
        var finalized = await FinalizeSignedSepaCoreAsync(
            prospectId,
            preference,
            contactEmail,
            ct);
        if (!finalized)
        {
            return;
        }

        var marked = await mandateClient.MarkSentToAkuiteoAsync(accountId, ct);
        if (!marked)
        {
            logger.LogWarning(
                "Mandat did not mark SEPA mandate as sent to Akuiteo for account {AccountId}",
                accountId);
            return;
        }

        await prospectService.CompleteStepAsync(
            accountId,
            new Models.Requests.CompleteStepRequest { StepName = PaymentMethodStepName },
            ct,
            SystemUserId);

        logger.LogInformation(
            "Completed PaymentMethod step for account {AccountId} after signed SEPA finalization",
            accountId);
    }

    /// <summary>
    /// Executes the signed SEPA document and account operations owned by one Mandat finalization lease.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="preference">The signed Mandat payment preference response.</param>
    /// <param name="contactEmail">The authenticated user email used for document audit headers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when every Akuiteo operation succeeded; otherwise false.</returns>
    private async Task<bool> FinalizeSignedSepaCoreAsync(
        int prospectId,
        MandatePaymentPreferenceResponse preference,
        string? contactEmail,
        CancellationToken ct)
    {
        var accountId = preference.AccountId!.Value;
        var iban = preference.Iban;
        var bic = preference.Bic;
        if (string.IsNullOrWhiteSpace(iban)
            || string.IsNullOrWhiteSpace(bic))
        {
            logger.LogWarning(
                "Cannot finalize signed SEPA mandate for account {AccountId}: Mandat did not return the persisted IBAN and BIC",
                accountId);
            return false;
        }

        logger.LogInformation(
            "Starting signed SEPA orchestration for prospect {ProspectId}, account {AccountId}, ribDocumentId {RibDocumentId}, signedMandateDocumentId {SignedMandateDocumentId}",
            prospectId,
            accountId,
            preference.RibDocumentId,
            preference.SignedMandateDocumentId);

        var rib = await prospectClient.GetDocumentAsync(prospectId, preference.RibDocumentId!.Value, ct);
        if (rib is null)
        {
            logger.LogWarning(
                "Cannot upload signed SEPA documents for account {AccountId}: RIB document {DocumentId} was not found",
                accountId,
                preference.RibDocumentId.Value);
            return false;
        }

        logger.LogInformation(
            "Retrieved RIB document {DocumentId} for prospect {ProspectId}: fileName {FileName}, contentType {ContentType}, size {ContentLength}",
            preference.RibDocumentId.Value,
            prospectId,
            rib.FileName,
            rib.ContentType,
            rib.Content.Length);

        var accountNumber = await prospectClient.GetAkuiteoAccountNumberByProspectIdAsync(prospectId, ct);
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            logger.LogWarning("Cannot upload signed SEPA documents for account {AccountId}: Akuiteo account number was not found", accountId);
            return false;
        }

        var signedMandateContent = Convert.FromBase64String(preference.SignedMandatePdfBase64!);
        var signedMandateFileName = BuildSignedMandateFileName(accountNumber);
        var signedMandateContentType = NormalizeSignedMandateContentType(
            preference.SignedMandateContentType,
            signedMandateFileName);

        var ribUploaded = await registryClient.UploadAkuiteoDocumentAsync(accountNumber, rib, ct);
        if (!ribUploaded)
        {
            logger.LogWarning("RIB Akuiteo upload failed for signed SEPA documents on account {AccountId}", accountId);
            return false;
        }

        logger.LogInformation(
            "RIB document {DocumentId} uploaded to Akuiteo for account {AccountId} under account number {AccountNumber}",
            preference.RibDocumentId.Value,
            accountId,
            accountNumber);

        var signedMandateProspectDocumentId = await EnsureSignedMandateDocumentIdAsync(
            prospectId,
            accountId,
            preference,
            signedMandateContent,
            signedMandateContentType,
            signedMandateFileName,
            contactEmail,
            ct);
        if (!signedMandateProspectDocumentId.HasValue)
        {
            return false;
        }

        var mandate = new ProspectDocumentContentResponse(
            signedMandateContent,
            signedMandateContentType,
            signedMandateFileName);
        var mandateUploaded = await registryClient.UploadAkuiteoDocumentAsync(accountNumber, mandate, ct);

        if (!mandateUploaded)
        {
            logger.LogWarning("Akuiteo upload failed for signed SEPA documents on account {AccountId}", accountId);
            return false;
        }

        logger.LogInformation(
            "Signed mandate document id {SignedMandateDocumentId} uploaded to Akuiteo for account {AccountId} under account number {AccountNumber}",
            signedMandateProspectDocumentId.Value,
            accountId,
            accountNumber);

        var paymentDetailsUpdated = await UpdateSepaPaymentDetailsAsync(
            accountId,
            iban,
            bic,
            ct);
        if (!paymentDetailsUpdated)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts and sends the SEPA banking details, then activates direct debit in Akuiteo.
    /// </summary>
    /// <param name="accountId">The Registry account identifier.</param>
    /// <param name="iban">The persisted IBAN returned by Mandat for the signed mandate.</param>
    /// <param name="bic">The persisted BIC returned by Mandat for the signed mandate.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True when both Registry account operations succeed; otherwise false.</returns>
    private async Task<bool> UpdateSepaPaymentDetailsAsync(
        int accountId,
        string iban,
        string bic,
        CancellationToken ct)
    {
        var extractedBankDetails = await mandateClient.ExtractBankDetailsAsync(
            iban,
            bic,
            ct);
        if (extractedBankDetails is null)
        {
            logger.LogWarning(
                "Mandat returned BadRequest during bank-details extraction for SEPA payment preference on account {AccountId}; see the downstream validation warning for details",
                accountId);
            return false;
        }

        var bankingInformation = AkuiteoBankingInformationMapper.ToBankingInformationRequest(extractedBankDetails);
        var bankingInformationUpdated = await registryClient.UpdateAkuiteoBankingInformationAsync(
            accountId,
            bankingInformation,
            ct);
        if (!bankingInformationUpdated)
        {
            logger.LogWarning(
                "Registry did not update Akuiteo banking information for SEPA payment preference on account {AccountId}",
                accountId);
            return false;
        }

        var paymentMethodUpdated = await registryClient.PatchAkuiteoAccountPaymentMethodAsync(
            accountId,
            AkuiteoBankingInformationMapper.ToDirectDebitPaymentMethodRequest(),
            ct);
        if (!paymentMethodUpdated)
        {
            logger.LogWarning(
                "Registry did not set direct debit for SEPA payment preference on account {AccountId}",
                accountId);
            return false;
        }

        logger.LogInformation(
            "Updated Akuiteo banking information and direct-debit payment method for SEPA payment preference on account {AccountId}",
            accountId);
        return true;
    }

    /// <summary>
    /// Ensures the signed mandate document is persisted in Prospect and its identifier is saved in Mandat.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="preference">The internal Mandat payment preference response.</param>
    /// <param name="content">The signed mandate PDF content.</param>
    /// <param name="contentType">The signed mandate content type.</param>
    /// <param name="fileName">The signed mandate file name.</param>
    /// <param name="contactEmail">The authenticated user email used for document audit headers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The existing or newly created Prospect document identifier.</returns>
    private async Task<int?> EnsureSignedMandateDocumentIdAsync(
        int prospectId,
        int accountId,
        MandatePaymentPreferenceResponse preference,
        byte[] content,
        string contentType,
        string fileName,
        string? contactEmail,
        CancellationToken ct)
    {
        if (int.TryParse(preference.SignedMandateDocumentId, NumberStyles.None, CultureInfo.InvariantCulture, out var existingDocumentId))
        {
            logger.LogInformation(
                "Reusing existing signed mandate document id {SignedMandateDocumentId} for account {AccountId}",
                existingDocumentId,
                accountId);
            return existingDocumentId;
        }

        var signedMandateProspectDocumentId = await UploadSignedMandateToProspectAsync(
            prospectId,
            content,
            contentType,
            fileName,
            contactEmail,
            ct);
        if (!signedMandateProspectDocumentId.HasValue)
        {
            logger.LogWarning("Signed SEPA document persistence failed before Akuiteo upload for account {AccountId}", accountId);
            return null;
        }

        logger.LogInformation(
            "Signed mandate stored in Prospect for account {AccountId} with new document id {SignedMandateDocumentId}",
            accountId,
            signedMandateProspectDocumentId.Value);

        var savedDocumentId = await mandateClient.SaveSignedMandateDocumentIdAsync(
            accountId,
            ct,
            signedMandateProspectDocumentId.Value.ToString(CultureInfo.InvariantCulture));
        if (!savedDocumentId)
        {
            logger.LogWarning("Mandat did not save signed mandate document identifier for account {AccountId}", accountId);
            return null;
        }

        logger.LogInformation(
            "Saved signed mandate document id {SignedMandateDocumentId} in Mandat for account {AccountId}",
            signedMandateProspectDocumentId.Value,
            accountId);

        return signedMandateProspectDocumentId.Value;
    }

    /// <summary>
    /// Uploads the signed mandate PDF to Prospect blob storage and document metadata table.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="content">The signed mandate PDF content.</param>
    /// <param name="contentType">The signed mandate content type.</param>
    /// <param name="fileName">The signed mandate file name.</param>
    /// <param name="contactEmail">The authenticated user email used for document audit headers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created Prospect document identifier, or null when Prospect rejects the upload.</returns>
    private async Task<int?> UploadSignedMandateToProspectAsync(
        int prospectId,
        byte[] content,
        string contentType,
        string fileName,
        string? contactEmail,
        CancellationToken ct)
    {
        await using var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return await prospectClient.UploadDocumentAsync(
            prospectId,
            SystemUserId,
            contactEmail,
            SignedMandateDocumentType,
            file,
            ct);
    }

    /// <summary>
    /// Builds the signed mandate file name expected by downstream document storage.
    /// </summary>
    /// <param name="accountNumber">The Akuiteo account number.</param>
    /// <returns>The signed mandate file name.</returns>
    private static string BuildSignedMandateFileName(string accountNumber)
    {
        return $"mandat-{accountNumber}-signature.pdf";
    }

    /// <summary>
    /// Normalizes the signed mandate content type for downstream validators.
    /// </summary>
    /// <param name="contentType">The content type returned by GetAccept.</param>
    /// <param name="fileName">The signed mandate file name.</param>
    /// <returns>The content type to use for Prospect and Akuiteo uploads.</returns>
    private static string NormalizeSignedMandateContentType(string? contentType, string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(contentType)
                || "application/octet-stream".Equals(contentType, StringComparison.OrdinalIgnoreCase)
                || "binary/octet-stream".Equals(contentType, StringComparison.OrdinalIgnoreCase)))
        {
            return PdfContentType;
        }

        return string.IsNullOrWhiteSpace(contentType) ? PdfContentType : contentType;
    }

    /// <inheritdoc />
    public async Task<bool> SetOtherAsync(int prospectId, string contactEmail, int currentUserId, CancellationToken ct)
    {
        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Payment preference OTHER requested for unknown prospect {ProspectId}", prospectId);
            return false;
        }

        var saved = await mandateClient.SetOtherAsync(prospect.AccountId, contactEmail, ct);
        if (!saved)
        {
            logger.LogWarning(
                "Mandat refused payment preference OTHER for account {AccountId} linked to prospect {ProspectId}",
                prospect.AccountId,
                prospectId);
            return false;
        }

        await prospectService.CompleteStepAsync(
            prospect.AccountId,
            new Models.Requests.CompleteStepRequest { StepName = PaymentMethodStepName },
            ct,
            currentUserId);

        var collabReceivers = paymentPreferenceNotificationService.GetCollabEmailReceivers();
        await paymentPreferenceNotificationService.SendAsync(
            prospectId,
            prospect.Email,
            collabReceivers,
            ct);

        return true;
    }

    /// <inheritdoc />
    public async Task<SepaPaymentPreferenceOrchestrationResult> SetSepaAsync(
        int prospectId,
        Models.Requests.SepaPaymentPreferenceRequest request,
        string contactEmail,
        string contactFirstName,
        string contactLastName,
        int currentUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.File);

        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Payment preference SEPA requested for unknown prospect {ProspectId}", prospectId);
            return SepaPaymentPreferenceOrchestrationResult.FromOutcome(
                SepaPaymentPreferenceOrchestrationOutcome.NotFound);
        }

        var isSignatory = await prospectClient.IsProspectSignatoryAsync(prospectId, currentUserId, ct);
        if (!isSignatory)
        {
            logger.LogWarning(
                "Contact {ContactEmail} is not a signatory authorized to start SEPA signature for prospect {ProspectId}",
                contactEmail,
                prospectId);
            return SepaPaymentPreferenceOrchestrationResult.FromOutcome(
                SepaPaymentPreferenceOrchestrationOutcome.Forbidden);
        }

        var uploadedDocumentId = await prospectClient.UploadDocumentAsync(
            prospectId,
            currentUserId,
            contactEmail,
            RibDocumentType,
            request.File,
            ct);
        if (!uploadedDocumentId.HasValue)
        {
            return SepaPaymentPreferenceOrchestrationResult.FromOutcome(
                SepaPaymentPreferenceOrchestrationOutcome.NotFound);
        }

        var signatureUrl = await mandateClient.SetSepaAsync(
            prospect.AccountId,
            new Models.Internal.MandateSepaPaymentPreferenceRequest
            {
                DocumentId = uploadedDocumentId.Value,
                AccountHolder = request.AccountHolder,
                Address = request.Address,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                Country = request.Country,
                PostalCode = request.PostalCode,
                Iban = request.Iban,
                Bic = request.Bic,
                RecipientEmail = contactEmail,
                RecipientFirstName = contactFirstName,
                RecipientLastName = contactLastName
            },
            ct);

        if (string.IsNullOrWhiteSpace(signatureUrl))
        {
            logger.LogWarning(
                "Mandat refused SEPA payment preference for account {AccountId} linked to prospect {ProspectId}",
                prospect.AccountId,
                prospectId);
            return SepaPaymentPreferenceOrchestrationResult.FromOutcome(
                SepaPaymentPreferenceOrchestrationOutcome.MandateFailed);
        }

        await prospectClient.MarkPaymentMethodInProgressAsync(prospectId, ct);
        return SepaPaymentPreferenceOrchestrationResult.Completed(signatureUrl);
    }

    /// <inheritdoc />
    public async Task<bool> ResetAsync(int prospectId, CancellationToken ct)
    {
        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Payment preference reset requested for unknown prospect {ProspectId}", prospectId);
            return false;
        }

        var reset = await mandateClient.ResetAsync(prospect.AccountId, ct);
        if (!reset)
        {
            logger.LogWarning(
                "Mandat refused payment preference reset for account {AccountId} linked to prospect {ProspectId}",
                prospect.AccountId,
                prospectId);
            return false;
        }

        await prospectClient.ResetStepAsync(prospectId, PaymentMethodStepName, ct);
        return true;
    }
}
