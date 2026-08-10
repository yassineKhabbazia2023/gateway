namespace ApiGateway.FeatureFlags.Models;

public sealed class FeatureContext
{
    public string? Email { get; init; }

    public string? ContactId { get; init; }

    public static FeatureContext? FromEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : new FeatureContext { Email = email.Trim() };

    public static FeatureContext? FromContactId(string? contactId)
        => string.IsNullOrWhiteSpace(contactId) ? null : new FeatureContext { ContactId = contactId.Trim() };
}
