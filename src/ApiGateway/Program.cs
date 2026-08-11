using ApiGateway.Configuration;
using ApiGateway.Extensions;
using ApiGateway.FeatureFlags.Extensions;
using ApiGateway.LocalRouting;
using ApiGateway.Middlewares;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Personal, unversioned file: declares the microservices started on the workstation.
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        Microsoft.Extensions.Logging.ActivityTrackingOptions.TraceId |
        Microsoft.Extensions.Logging.ActivityTrackingOptions.SpanId;
});
// In every deployed environment the CORS headers are produced by the App Service
// CORS module, not by this application. A gateway started on a developer machine
// has no such front, so a local front served from another origin needs the policy
// below. Enabled only when LocalCorsOrigins is set, which never happens outside
// appsettings.Development.json.
const string LocalCorsPolicy = "LocalCors";
const string LocalCorsOriginsSection = "LocalCorsOrigins";
var localCorsOrigins = builder.Configuration.GetSection(LocalCorsOriginsSection).Get<string[]>() ?? [];

if (localCorsOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(LocalCorsPolicy, policy => policy
        .WithOrigins(localCorsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}

builder.Services.AddApiGatewayServices(builder.Configuration);
await builder.Services.AddAuthenticationServicesAsync(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<GatewayExceptionMiddleware>();
builder.Services.AddHttpLogging(o =>
{
});

// In a deployed environment the Ocelot configuration is a file produced by the pipeline;
// on a workstation it is built from src/Config, the private hosts not being routable.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddLocalRouting(builder.Configuration, builder.Environment);
}
else
{
    builder.Configuration.AddJsonConfiguration();
}

var app = builder.Build();
await app.UseFeatureFlagsAsync();

app.UseHttpLogging();
app.UseRouting();

// Must sit before the authentication and Ocelot middlewares so that preflight
// requests are answered without a token and without route matching.
if (localCorsOrigins.Length > 0)
{
    app.UseCors(LocalCorsPolicy);
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName.ToLower() == "local")
{
    app.UseSwaggerForOcelotUI(opt =>
    {
        opt.ServerOcelot = "/desktop";
        opt.PathToSwaggerGenerator = "/swagger/docs";
    }, c =>
    {
        c.EnableTryItOutByDefault();
        c.RoutePrefix = "api";
    });
}
else
{
    app.UseSwaggerForOcelotUI(opt =>
    {
        opt.ServerOcelot = "/desktop";
        opt.DownstreamSwaggerEndPointBasePath = "/desktop/swagger/docs";
    }, c =>
    {
        c.EnableTryItOutByDefault();
        c.RoutePrefix = "api";
    });

    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.CatchExceptions();
    });
}

// Token revocation check - must be before authentication
app.UseMiddleware<TokenRevocationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllers();
    _ = endpoints.MapHealthChecks("health");
});

app.UseSwagger();

var config = new OcelotPipelineConfiguration
{
    AuthorizationMiddleware
                = async (downStreamContext, next) =>
                {
                    await ApiGateway.Middlewares.AuthorizationMiddleware.AuthorizationFilter(downStreamContext, next);
                },
    PreErrorResponderMiddleware = async (context, next) =>
    {
        var exceptionMiddleware = context.RequestServices.GetRequiredService<GatewayExceptionMiddleware>();
        await exceptionMiddleware.InvokeAsync(context, next);
    }
};

await app.UseOcelot(config);
app.UseRouting();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});
app.Run();

public partial class Program { }
