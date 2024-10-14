using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.Contact.Models;
using ApiGateway.Extensions;
using ApiGateway.Helpers;

namespace ApiGateway.DelegatingHandlers;

public class ContactHandler : DelegatingHandler
{
    private readonly IServiceScopeFactory _serviceProviderFactory;
    private readonly ILogger<ContactHandler> _logger;

    public ContactHandler(
        IServiceScopeFactory serviceProviderFactory,
        ILogger<ContactHandler> logger
        )
    {
        _serviceProviderFactory = serviceProviderFactory;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
    CancellationToken cancellationToken)
    {
        var contactId = await GetCurrentUser(request);
        var contactEmail = GetUserEmail(request);

        request.PrepareRequestHeader(contactEmail, contactId);

        return await base.SendAsync(request,
            cancellationToken);
    }

    private string GetUserEmail(HttpRequestMessage request)
    {
        var token = JwtHelper.ExtractBearerToken(request);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Le jeton d'authentification (bearer token) est absent de l'en-tête de la requête : {downstream}", request.RequestUri);
            return null;
        }

        var userEmail = JwtHelper.ExtractUserEmailFromToken(token);

        return userEmail;
    } 

    public async Task<string?> GetCurrentUser(HttpRequestMessage request)
    {
        var token = JwtHelper.ExtractBearerToken(request);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Le jeton d'authentification (bearer token) est absent de l'en-tête de la requête : {downstream}", request.RequestUri);
            return null;
        }

        var userEmail = JwtHelper.ExtractUserEmailFromToken(token);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            _logger.LogWarning("Le jeton d'authentification ne contient  pas de claim de type Email");
            return null;
        }

        // Common issue in .NET Core applications related to dependency injection.
        // The core of the problem lies in our attempt to inject a service with a lifespan limited to a single request
        // (IContactService, a scoped service) into a component that has an application-wide lifespan (our ContactHandler, a singleton).
        var scope = _serviceProviderFactory.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var contactId = await cacheService.GetAsync(userEmail);
        if (string.IsNullOrWhiteSpace(contactId))
        {
            var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();
            contactId = await contactService.GetContactIdAsync(userEmail);

            if (string.IsNullOrWhiteSpace(contactId))
            {
                _logger.LogDebug("Le contact avec l'adresse e-mail : {email} est introuvable.", userEmail);
                return null;
            }

            await cacheService.SetContactIdAsync(userEmail, contactId);
        }

        return contactId;
    }
}