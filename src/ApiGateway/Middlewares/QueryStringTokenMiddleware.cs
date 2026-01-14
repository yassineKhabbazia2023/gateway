using Microsoft.Net.Http.Headers;

namespace ApiGateway.Middlewares;

/// <summary>
/// Middleware that extracts a JWT token from the query string and adds it to the Authorization header.
/// This enables browser-based access to protected endpoints (e.g., opening a PDF in a new tab).
/// Only applies to specific whitelisted paths to minimize security exposure.
/// </summary>
public class QueryStringTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QueryStringTokenMiddleware> _logger;

    private static readonly string[] AllowedPaths =
    [
        "/gtw/pennylane/api/pennylane/terms/content"
    ];

    public QueryStringTokenMiddleware(RequestDelegate next, ILogger<QueryStringTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        // Only process whitelisted paths
        if (path != null && IsAllowedPath(path))
        {
            // Check if token is in query string and no Authorization header exists
            if (context.Request.Query.TryGetValue("token", out var encodedToken)
                && !string.IsNullOrWhiteSpace(encodedToken)
                && !context.Request.Headers.ContainsKey(HeaderNames.Authorization))
            {
                try
                {
                    var token = DecodeBase64Token(encodedToken!);
                    context.Request.Headers[HeaderNames.Authorization] = $"Bearer {token}";
                }
                catch (FormatException)
                {
                    _logger.LogWarning("Invalid token format for path {Path}", path);
                }
            }
        }

        await _next(context);
    }

    private static bool IsAllowedPath(string path)
    {
        return AllowedPaths.Any(allowed => path.Contains(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static string DecodeBase64Token(string encodedToken)
    {
        var base64 = encodedToken
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
