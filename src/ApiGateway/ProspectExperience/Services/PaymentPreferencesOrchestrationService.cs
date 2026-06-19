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
