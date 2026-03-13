using ApiGateway.Configuration;
using ApiGateway.Extensions;
using ApiGateway.FeatureFlags.Extensions;
using ApiGateway.Middlewares;
using Microsoft.FeatureManagement;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddFeatureManagement();
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
builder.Services.RegisterApplicationInsights(builder.Configuration);

builder.Logging.AddApplicationInsights();

builder.Services.AddHttpLogging(o =>
{
});


builder.Configuration.AddJsonConfiguration();

var app = builder.Build();

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
        await ApiGateway.Middlewares.GatewayExceptionMiddleware.ExceptionFilter(context, next);
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
