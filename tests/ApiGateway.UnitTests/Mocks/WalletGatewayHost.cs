using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiGateway.Account;
using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.DelegatingHandlers;
using ApiGateway.Identity;
using ApiGateway.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

namespace ApiGateway.UnitTests.Mocks;

/// <summary>
/// Runs the real Ocelot routing, JWT validation, authorization and contact pipeline against a local Account stub.
/// External identity/contact services are mocked; no production credentials or network services are used.
/// </summary>
public sealed class WalletGatewayHost : IAsyncLifetime
{
    private const string Issuer = "wallet-routing-tests";
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));
    private WebApplication _gateway = null!;
    private WebApplication _account = null!;

    /// <summary>Gets the HTTP client targeting the local Gateway.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Gets all HTTP requests received by the downstream stub, including unexpected paths.</summary>
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    /// <summary>Gets the mocked contact service.</summary>
    public Mock<IContactService> ContactService { get; } = new();

    /// <summary>Gets the mocked identity authorization service.</summary>
    public Mock<IIdentityService> IdentityService { get; } = new();

    /// <summary>Gets or sets the downstream HTTP status.</summary>
    public int ResponseStatus { get; set; } = StatusCodes.Status200OK;

    /// <summary>Gets or sets the downstream response body, forwarded verbatim.</summary>
    public string ResponseBody { get; set; } = """
        {"accounts":{"items":[{"accountId":7,"legalName":"Example"}],"currentPage":1,"totalItems":121,"totalPage":2},"regularEntitiesCount":5000,"favorites":null,"statistics":null}
        """;

    /// <summary>Starts isolated loopback listeners and loads the Wallet and legacy routes from production configuration.</summary>
    public async Task InitializeAsync()
    {
        var accountBuilder = CreateBuilder();
        _account = accountBuilder.Build();
        _account.Run(async context =>
        {
            var captured = new HttpRequestMessage(new HttpMethod(context.Request.Method),
                $"http://localhost{context.Request.Path}{context.Request.QueryString}");
            foreach (var header in context.Request.Headers)
            {
                captured.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            Requests.Enqueue(captured);
            context.Response.StatusCode = ResponseStatus;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(ResponseBody);
        });
        await _account.StartAsync();

        var builder = CreateBuilder();
        var config = ReadRoutes();
        var downstream = new Uri(_account.Urls.Single());
        foreach (var route in config["Routes"]!.Cast<JObject>())
        {
            route["DownstreamScheme"] = "http";
            route["DownstreamHostAndPorts"] = new JArray(new JObject
            {
                ["Host"] = downstream.Host,
                ["Port"] = downstream.Port,
            });
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(config.ToString()));
        builder.Configuration.AddJsonStream(stream);
        var authentication = builder.Services.AddAuthentication();
        foreach (var scheme in new[] { "AAD", "GIGYA MyPulse v2" })
        {
            authentication.AddJwtBearer(scheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Issuer,
                    ValidAudience = scheme,
                    IssuerSigningKey = SigningKey,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });
        }

        var contact = new ApiGateway.Contact.Models.Contact { Id = 42, Type = "Collaborateur" };
        ContactService.Setup(service => service.GetContactIdAsync("wallet@example.com")).ReturnsAsync("42");
        ContactService.Setup(service => service.GetContactAsync("wallet@example.com")).ReturnsAsync(contact);
        IdentityService.Setup(service => service.ValidateCollaborator(It.IsAny<HttpContext>())).Returns(true);
        IdentityService.Setup(service => service.ValidateCustomerAsync("wallet@example.com")).ReturnsAsync(true);
        builder.Services.AddSingleton(ContactService.Object);
        builder.Services.AddSingleton(IdentityService.Object);
        builder.Services.AddSingleton(Mock.Of<ICacheService>());
        builder.Services.AddSingleton(new Mock<IAccountService>(MockBehavior.Strict).Object);
        builder.Services.AddSingleton<GatewayExceptionMiddleware>();
        builder.Services.AddHttpClient();
        builder.Services.AddOcelot(builder.Configuration)
            .AddDelegatingHandler<TraceContextHandler>(true)
            .AddDelegatingHandler<ContactHandler>(true)
            .AddDelegatingHandler<DownstreamExceptionHandler>(true);

        _gateway = builder.Build();
        _gateway.UseAuthentication();
        await _gateway.UseOcelot(new OcelotPipelineConfiguration
        {
            AuthorizationMiddleware = ApiGateway.Middlewares.AuthorizationMiddleware.AuthorizationFilter,
            PreErrorResponderMiddleware = (context, next) =>
                context.RequestServices.GetRequiredService<GatewayExceptionMiddleware>().InvokeAsync(context, next),
        });
        await _gateway.StartAsync();
        Client = _gateway.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        Client.BaseAddress = new Uri(_gateway.Urls.Single());
    }

    /// <summary>Creates a signed authenticated request, optionally carrying forged browser identity headers.</summary>
    /// <param name="path">The upstream path and query.</param>
    /// <param name="scheme">The configured authentication scheme.</param>
    /// <param name="spoofIdentity">Whether to send forged identity headers.</param>
    /// <returns>The request to send through Ocelot.</returns>
    public static HttpRequestMessage CreateRequest(string path, string scheme = "AAD", bool spoofIdentity = false)
    {
        var token = new JwtSecurityToken(Issuer, scheme,
            [new Claim("email", "wallet@example.com"), new Claim(ClaimTypes.Role, scheme == "AAD" ? "Collaborator" : "Customer")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        request.Headers.Add("X-Correlation-Id", "wallet-routing-test");
        if (spoofIdentity)
        {
            request.Headers.Add("CurrentUser", "999");
            request.Headers.Add("ContactEmail", "forged@example.com");
            request.Headers.Add("ContactType", "Administrator");
        }

        return request;
    }

    /// <summary>Stops Account to exercise Ocelot's connection failure response.</summary>
    public Task StopAccountAsync() => _account.StopAsync();

    /// <summary>Releases clients, captured requests and both local hosts.</summary>
    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_gateway is not null)
        {
            await _gateway.DisposeAsync();
        }

        if (_account is not null)
        {
            await _account.DisposeAsync();
        }

        foreach (var request in Requests)
        {
            request.Dispose();
        }
    }

    /// <summary>Creates a quiet host with an automatically assigned loopback port.</summary>
    /// <returns>The host builder.</returns>
    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return builder;
    }

    /// <summary>Loads actual production mappings for the optimized Wallet, statistics and legacy account list.</summary>
    /// <returns>The Ocelot configuration containing the routes under test.</returns>
    private static JObject ReadRoutes()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Config")))
        {
            directory = directory.Parent;
        }

        var folder = Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Gateway configuration not found."), "src", "Config");
        var routes = new[] { "ocelot.wallet.json", "ocelot.account.json" }
            .SelectMany(file => JObject.Parse(File.ReadAllText(Path.Combine(folder, file)))["Routes"]!.Cast<JObject>())
            .Where(route => route["UpstreamPathTemplate"]!.Value<string>() is
                "/gtw/wallet/api/currentuser" or
                "/gtw/wallet/api/statistics/currentuser/{everything}" or
                "/gtw/account/api/accounts/currentuser" or
                "/gtw/account/api/accounts/currentuser?pageNumber={abc}&pageSize={xyz}&search={ghi}");
        return new JObject { ["Routes"] = new JArray(routes) };
    }
}
