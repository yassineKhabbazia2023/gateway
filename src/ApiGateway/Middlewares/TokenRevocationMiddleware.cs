using ApiGateway.Exceptions;
using ApiGateway.Helpers;
using ApiGateway.TokenRevocation;
using System.IdentityModel.Tokens.Jwt;

namespace ApiGateway.Middlewares;

/// <summary>
/// Middleware to check if a JWT token has been revoked
/// Executes before JWT validation
/// </summary>
public class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRevocationMiddleware> _logger;

    public TokenRevocationMiddleware(RequestDelegate next, ILogger<TokenRevocationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ITokenRevocationCache revocationCache)
    {
        // Skip revocation check for certain paths (like login, logout endpoints)
        if (ShouldSkipRevocationCheck(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Try to extract the token from Authorization header
        var token = JwtHelper.ExtractBearerToken(context.Request);

        if (string.IsNullOrWhiteSpace(token))
        {
            // No token, let the authentication middleware handle it
            await _next(context);
            return;
        }

        try
        {
            // Decode the token WITHOUT validating signature or expiration
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Extract token ID - support jti (standard JWT), uti (Azure AD), or sub+iat (Gigya)
            string tokenId;

            var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
            var utiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "uti");

            if (jtiClaim != null && !string.IsNullOrWhiteSpace(jtiClaim.Value))
            {
                // Standard JWT with jti claim
                tokenId = jtiClaim.Value;
                _logger.LogDebug("[TOKEN-REVOKE] Using jti claim as token identifier: {TokenId}", tokenId);
            }
            else if (utiClaim != null && !string.IsNullOrWhiteSpace(utiClaim.Value))
            {
                // Azure AD token with uti claim
                tokenId = utiClaim.Value;
                _logger.LogDebug("[TOKEN-REVOKE] Using uti claim as token identifier: {TokenId}", tokenId);
            }
            else
            {
                // Fallback: use sub + iat for Gigya tokens or other tokens without jti/uti
                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
                var iatClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);

                if (subClaim != null && !string.IsNullOrWhiteSpace(subClaim.Value) &&
                    iatClaim != null && !string.IsNullOrWhiteSpace(iatClaim.Value))
                {
                    // Gigya token: combine sub (user ID) + iat (issued at timestamp)
                    tokenId = $"{subClaim.Value}:{iatClaim.Value}";
                    _logger.LogDebug("[TOKEN-REVOKE] Using sub:iat as token identifier: {TokenId}", tokenId);
                }
                else
                {
                    _logger.LogWarning("[TOKEN-REVOKE] Token does not contain jti, uti, or sub+iat claims - skipping revocation check");
                    await _next(context);
                    return;
                }
            }

            // Check if token is revoked
            if (revocationCache.IsTokenRevoked(tokenId))
            {
                _logger.LogWarning("[TOKEN-REVOKE] Revoked token detected. Token ID: {TokenId}", tokenId);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new Pulse.ExceptionMiddleware.Model.ErrorResponse()
                {
                    ErrorCode = Errors.UnauthorizedCode,
                    ErrorMessage = Errors.UnauthorizedMessage
                });

                return;
            }

            // Token is not revoked, continue
            await _next(context);
        }
        catch (Exception ex)
        {
            // If token decoding fails, let the authentication middleware handle it
            _logger.LogDebug(ex, "[TOKEN-REVOKE] Failed to decode token for revocation check");
            await _next(context);
        }
    }

    private static bool ShouldSkipRevocationCheck(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;

        // Skip revocation check for authentication and health endpoints
        // Use EndsWith to prevent bypass attacks (e.g., /gtw/authentication/api/login/bypass would NOT match)
        return pathValue.EndsWith("/gtw/authentication/api/login") ||
               pathValue.EndsWith("/gtw/authentication/api/logout") ||
               pathValue.EndsWith("/gtw/authentication/api/refreshtoken") ||
               pathValue.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
               pathValue.StartsWith("/health/", StringComparison.OrdinalIgnoreCase) ||
               pathValue.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }
}
