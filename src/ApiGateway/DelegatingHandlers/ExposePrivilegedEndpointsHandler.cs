using ApiGateway.Exceptions;
using System.Net;

namespace ApiGateway.DelegatingHandlers;

public class ExposePrivilegedEndpointsHandler : DelegatingHandler
{
    private readonly ILogger<ExposePrivilegedEndpointsHandler> _logger;
    private readonly bool _isEnabled;
    public const string ExposePrivilegedEndpoints = nameof(ExposePrivilegedEndpoints);

    public ExposePrivilegedEndpointsHandler(IConfiguration configuration, ILogger<ExposePrivilegedEndpointsHandler> logger)
    {
        _isEnabled = configuration.GetValue<bool?>(ExposePrivilegedEndpoints) ?? false;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_isEnabled)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        _logger.LogWarning($"[Response]: 403 - [Handler]: ExposePrivilegedEndpointsHandler - [Function]: SendAsync - [Reason]: Access Forbidden to privileged endpoints on this environment!");
        return new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new { ErrorMessage = Errors.UnauthorizedExposePrivilegedEndpointsCode, ErrorCode = Errors.UnauthorizedExposePrivilegedEndpointsMessage })
        };
    }
}
