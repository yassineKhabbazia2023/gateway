using ApiGateway.Account;
using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.DelegatingHandlers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Mocks;

public class TestableContactHandler : ContactHandler
{
    private readonly HttpMessageHandler _httpMessageHandler;

    public TestableContactHandler(
        IServiceScopeFactory _serviceProviderFactory,
        ILogger<ContactHandler> logger,
        IAccountService _accountService,
        HttpMessageHandler httpMessageHandler)
        : base(_serviceProviderFactory, logger, _accountService)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public async Task<HttpResponseMessage> TestSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        InnerHandler = _httpMessageHandler;
        return await SendAsync(request, cancellationToken);
    }
}

