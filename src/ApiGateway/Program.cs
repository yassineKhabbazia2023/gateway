using ApiGateway.Configuration;
using ApiGateway.Extensions;
using ApiGateway.FeatureFlags.Extensions;
using ApiGateway.Middlewares;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        Microsoft.Extensions.Logging.ActivityTrackingOptions.TraceId |
        Microsoft.Extensions.Logging.ActivityTrackingOptions.SpanId;
});
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<GatewayExceptionMiddleware>();
builder.Services.AddHttpLogging(o =>
{
});

builder.Configuration.AddJsonConfiguration();

var app = builder.Build();
await app.UseFeatureFlagsAsync();

app.UseHttpLogging();
app.UseRouting();

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
