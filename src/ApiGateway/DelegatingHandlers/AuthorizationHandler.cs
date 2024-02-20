namespace ApiGateway.DelegatingHandlers;

public class AuthorizationHandler : DelegatingHandler
{
    private readonly ILogger<AuthorizationHandler> _logger;

    public AuthorizationHandler(ILogger<AuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking if the user is authorized");
        // Custom logic here
        return await base.SendAsync(request,
            cancellationToken);

    }

}