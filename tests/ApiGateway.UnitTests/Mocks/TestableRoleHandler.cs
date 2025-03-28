using ApiGateway.DelegatingHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Mocks;

public class TestableRoleHandler : RoleHandler
{
    private readonly HttpMessageHandler _httpMessageHandler;

    public TestableRoleHandler(
        ILogger<RoleHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        HttpMessageHandler httpMessageHandler)
        : base(logger, httpContextAccessor)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public async Task<HttpResponseMessage> TestSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        InnerHandler = _httpMessageHandler;
        return await SendAsync(request, cancellationToken);
    }
}

