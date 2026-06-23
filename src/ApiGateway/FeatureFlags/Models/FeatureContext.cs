namespace ApiGateway.FeatureFlags.Models;

public sealed class FeatureContext
{
    private readonly string _email = null!;

    public required string Email
    {
        get => _email;
        init => _email = Guard.Against.NullOrWhiteSpace(value);
    }

    public static FeatureContext? FromEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : new FeatureContext { Email = email };
}
