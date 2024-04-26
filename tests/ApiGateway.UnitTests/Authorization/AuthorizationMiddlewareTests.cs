using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using ApiGateway.Authorization;
using ApiGateway.Contact;
using ApiGateway.Helpers;
using System.Collections.Generic;
using ApiGateway.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Azure.Core;
using Microsoft.Extensions.Primitives;
using System;
using Ocelot.Configuration;
using Ocelot.Values;
using System.Net;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationMiddlewareTests
{
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<IAuthorizationSevice> _mockAuthorizationService;

    public AuthorizationMiddlewareTests()
    {
        _mockContactService = new Mock<IContactService>(MockBehavior.Strict);
        _mockAuthorizationService = new Mock<IAuthorizationSevice>(MockBehavior.Strict);
    }

    [Fact]
    public async Task AuthorizationFilter_WhenHasRequiredPermissions_ShouldAllowsAccess()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>() { "COADMI001" })
            .Verifiable();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenRequiredClaimIsEmpty_ShouldAllowsAccess()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>();

        var httpContext = DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.RequestServices = new ServiceCollection()
           .AddSingleton(_mockContactService.Object)
           .AddSingleton(_mockAuthorizationService.Object)
           .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenUserEmailIsEmpty_ShouldReturnForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = string.Empty;
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .BuildServiceProvider();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenContactIdIsEmpty_ShoulsReturnForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(() => null!)
            .Verifiable();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains(httpContext.Items, x => x.Key.Equals("Errors"));
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationFilter_WhenPermissionsAreEmpty_ForbiddenRequest()
    {
        // Arrange
        var path = "/gtw/offer/api/subscription";
        var method = "GET";
        var contactEmail = "user-demo@kpmg.fr";
        var requiredClaims = new Dictionary<string, string>
        {
            { "GET", "CLADMI001,COADMI001" },
            { "POST", "CLPEN001,COINFO001" },
            { "PUT", "CLRAPP002,COEVPO01" },
        };

        var httpContext = DummyHttpContext(path, method, contactEmail, requiredClaims);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_mockContactService.Object)
            .AddSingleton(_mockAuthorizationService.Object)
            .BuildServiceProvider();

        _mockContactService.Setup(x => x.GetContactIdAsync(It.IsAny<string>()))
            .Callback<string>(email => email.Equals(contactEmail))
            .ReturnsAsync("90")
            .Verifiable();

        _mockAuthorizationService.Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<int, int?>((contactId, accountId) =>
            {
                contactId.Equals(contactId);
            })
            .ReturnsAsync(new List<string>())
            .Verifiable();

        // Act
        await AuthorizationMiddleware.AuthorizationFilter(httpContext, () => Task.CompletedTask);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        _mockContactService.VerifyAll();
        _mockAuthorizationService.VerifyAll();
    }

    private static DefaultHttpContext DummyHttpContext(
        string path,
        string method,
        string contactEmail,
        Dictionary<string, string> requiredClaims)
    {
        // Mock HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.Method = method;
        httpContext.Request.Headers.Authorization = new StringValues($"Bearer {GenerateDummyJwtToken(contactEmail)}");

        var downstreamRoute = new DownstreamRoute(
            key: "key",
            upstreamPathTemplate: new UpstreamPathTemplate("template", 1, true, ""),
            upstreamHeadersFindAndReplace: null,
            downstreamHeadersFindAndReplace: null,
            downstreamAddresses: null,
            serviceName: "serviceName",
            serviceNamespace: "serviceNamespace",
            httpHandlerOptions: null,
            useServiceDiscovery: false,
            enableEndpointEndpointRateLimiting: false,
            qosOptions: null,
            downstreamScheme: "http",
            requestIdKey: null,
            isCached: false,
            cacheOptions: null,
            loadBalancerOptions: null,
            rateLimitOptions: null,
            routeClaimsRequirement: requiredClaims,
            claimsToQueries: null,
            claimsToHeaders: null,
            claimsToClaims: null,
            claimsToPath: null,
            isAuthenticated: false,
            isAuthorized: false,
            authenticationOptions: null,
            downstreamPathTemplate: null,
            loadBalancerKey: null,
            delegatingHandlers: null,
            addHeadersToDownstream: null,
            addHeadersToUpstream: null,
            dangerousAcceptAnyServerCertificateValidator: false,
            securityOptions: null,
            downstreamHttpMethod: null,
            downstreamHttpVersion: null
        );

        httpContext.Items["DownstreamRoute"] = downstreamRoute;

        return httpContext;
    }

    private static string GenerateDummyJwtToken(string userEmail)
    {
        var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var claims = new Dictionary<string, string>
        {
            {"email", userEmail}
        };
        var payload = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(claims));
        var signature = "";

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(inputBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
