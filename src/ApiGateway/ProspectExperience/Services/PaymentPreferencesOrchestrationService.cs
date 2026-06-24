using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Responses;

namespace ApiGateway.ProspectExperience.Services;

/// <inheritdoc />
public sealed class PaymentPreferencesOrchestrationService(
    IProspectApiClient prospectClient,
    IMandatePaymentPreferencesClient mandateClient,
    IProspectService prospectService,
    ILogger<PaymentPreferencesOrchestrationService> logger) : IPaymentPreferencesOrchestrationService
{
    private const string PaymentMethodStepName = "PAYMENT_METHOD";
    private const string RibDocumentType = "RIB";

    /// <inheritdoc />
    public async Task<PaymentPreferenceResponse?> GetAsync(int prospectId, CancellationToken ct)
    {
        var prospect = await prospectClient.GetProspectAccountAsync(prospectId, ct);
        if (prospect is null)
        {
            logger.LogWarning("Payment preference requested for unknown prospect {ProspectId}", prospectId);
            return null;
        }

        return await mandateClient.GetAsync(prospect.AccountId, ct);
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
            prospectId,
            new Models.Requests.CompleteStepRequest { StepName = PaymentMethodStepName },
            ct,
            currentUserId);
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
