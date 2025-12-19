using ApiGateway.TokenRevocation;

namespace ApiGateway.DelegatingHandlers;

/// <summary>
/// DelegatingHandler to intercept logout responses and add revoked tokens to cache
/// Executes in the Ocelot pipeline to capture downstream responses
/// </summary>
public class LogoutRevocationHandler : DelegatingHandler
{
    private readonly ITokenRevocationCache _revocationCache;
    private readonly ILogger<LogoutRevocationHandler> _logger;

    public LogoutRevocationHandler(
        ITokenRevocationCache revocationCache,
        ILogger<LogoutRevocationHandler> logger)
    {
        _revocationCache = revocationCache ?? throw new ArgumentNullException(nameof(revocationCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Execute the request and get the response
        var response = await base.SendAsync(request, cancellationToken);

        // Check if this is a logout request with successful response
        if (IsLogoutRequest(request) && response.IsSuccessStatusCode)
        {
            _logger.LogInformation("[TOKEN-REVOKE] Logout response received. Headers: {Headers}",
                string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

            // Extract revocation headers
            if (response.Headers.TryGetValues("X-Revoked-Jti", out var jtiValues) &&
                response.Headers.TryGetValues("X-Revoked-Exp", out var expValues))
            {
                var jti = jtiValues.FirstOrDefault();
                var expUnixTimestamp = expValues.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(jti) &&
                    !string.IsNullOrWhiteSpace(expUnixTimestamp) &&
                    long.TryParse(expUnixTimestamp, out var unixTimestamp))
                {
                    try
                    {
                        var expiration = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;

                        // Add token to revocation cache
                        _revocationCache.AddRevokedToken(jti, expiration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[TOKEN-REVOKE] Failed to add revoked token to cache. JTI: {Jti}", jti);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "[TOKEN-REVOKE] Invalid revocation headers: X-Revoked-Jti={Jti}, X-Revoked-Exp={Exp}",
                        jti, expUnixTimestamp);
                }

                // Remove internal headers to prevent information disclosure
                response.Headers.Remove("X-Revoked-Jti");
                response.Headers.Remove("X-Revoked-Exp");
            }
            else
            {
                _logger.LogDebug("[TOKEN-REVOKE] Logout response does not contain revocation headers");
            }
        }

        return response;
    }

    private static bool IsLogoutRequest(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Post)
        {
            return false;
        }

        var path = request.RequestUri?.PathAndQuery?.ToLowerInvariant() ?? string.Empty;
        return path.Contains("/authentication/logout");
    }
}
