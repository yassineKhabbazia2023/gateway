using Microsoft.Extensions.Configuration;

namespace ApiGateway.ProspectExperience.Services;

/// <inheritdoc />
public sealed class PaymentPreferenceNotificationService(
    IProspectApiClient prospectClient,
    IConfiguration configuration,
    ILogger<PaymentPreferenceNotificationService> logger) : IPaymentPreferenceNotificationService
{
    private const string ReceiversKey = "PaymentPreference:Notifications:Receivers";

    /// <inheritdoc />
    public string[] GetCollabEmailReceivers()
    {
        var configuredReceivers = configuration[ReceiversKey];

        if (string.IsNullOrWhiteSpace(configuredReceivers))
        {
            logger.LogWarning(
                "Payment preference notification receiver configuration {ConfigurationKey} is empty",
                ReceiversKey);
            return [];
        }

        return configuredReceivers
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task SendAsync(
        int prospectId,
        string signatoryEmail,
        string[] collabEmailReceivers,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Starting payment preference notification orchestration for prospect {ProspectId}",
            prospectId);
        logger.LogInformation(
            "Payment preference signatory notification target is configured for prospect {ProspectId}",
            prospectId);
        logger.LogInformation(
            "Payment preference collaborator notification receiver count for prospect {ProspectId}: {ReceiverCount}",
            prospectId,
            collabEmailReceivers.Length);

        try
        {
            logger.LogInformation(
                "Calling Prospect payment preference notification endpoint for prospect {ProspectId}",
                prospectId);

            await prospectClient.SendPaymentPreferenceNotificationsAsync(
                prospectId,
                signatoryEmail,
                collabEmailReceivers,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send payment preference notifications for prospect {ProspectId}",
                prospectId);
        }
    }
}
