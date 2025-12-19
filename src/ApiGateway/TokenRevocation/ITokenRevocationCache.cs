namespace ApiGateway.TokenRevocation;

/// <summary>
/// Service to manage revoked JWT tokens cache
/// IMPORTANT: This cache must be shared across all instances in a scaled-out environment
/// </summary>
public interface ITokenRevocationCache
{
    /// <summary>
    /// Adds a revoked token to the cache with automatic TTL based on expiration
    /// </summary>
    /// <param name="jti">Token identifier in one of these formats:
    /// - jti claim value (standard JWT)
    /// - uti claim value (Azure AD tokens)
    /// - sub:iat combined (Gigya tokens, format: "{sub}:{iat}")</param>
    /// <param name="expiration">Token expiration date</param>
    void AddRevokedToken(string jti, DateTime expiration);

    /// <summary>
    /// Checks if a token is revoked
    /// </summary>
    /// <param name="jti">Token identifier in one of these formats:
    /// - jti claim value (standard JWT)
    /// - uti claim value (Azure AD tokens)
    /// - sub:iat combined (Gigya tokens, format: "{sub}:{iat}")</param>
    /// <returns>True if token is revoked, false otherwise</returns>
    bool IsTokenRevoked(string jti);
}
