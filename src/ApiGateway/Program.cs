using ApiGateway.Configuration;
using ApiGateway.Extensions;
using ApiGateway.Middlewares;
using Microsoft.FeatureManagement;
using Ocelot.Authentication.Middleware;
using Ocelot.Authorization.Middleware;
using Ocelot.Claims.Middleware;
using Ocelot.DownstreamPathManipulation.Middleware;
using Ocelot.DownstreamRouteFinder.Middleware;
using Ocelot.DownstreamUrlCreator.Middleware;
using Ocelot.Errors.Middleware;
using Ocelot.Headers.Middleware;
using Ocelot.LoadBalancer.Middleware;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using Ocelot.QueryStrings.Middleware;
using Ocelot.Request.Middleware;
using Ocelot.Responder.Middleware;
using Ocelot.WebSockets;
using System.Reflection.PortableExecutable;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddFeatureManagement();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
builder.Services.RegisterApplicationInsights(builder.Configuration);

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

app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionMiddleware();

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
                }
};

app.UseWebSockets();

//Override Ocelot Websockets pipeline to support authentication and authorization
app.MapWhen(httpContext => httpContext.WebSockets.IsWebSocketRequest,
    wenSocketsApp =>
    {
        wenSocketsApp.UseDownstreamContextMiddleware();
        wenSocketsApp.UseExceptionHandlerMiddleware();
        wenSocketsApp.UseResponderMiddleware();
        wenSocketsApp.UseDownstreamRouteFinderMiddleware();
        wenSocketsApp.UseMultiplexingMiddleware();
        wenSocketsApp.UseHttpHeadersTransformationMiddleware();
        wenSocketsApp.UseDownstreamRequestInitialiser();
        wenSocketsApp.UseAuthenticationMiddleware();
        wenSocketsApp.UseClaimsToClaimsMiddleware();
        wenSocketsApp.UseAuthorizationMiddleware();
        wenSocketsApp.UseClaimsToHeadersMiddleware();
        wenSocketsApp.UseClaimsToQueryStringMiddleware();
        wenSocketsApp.UseClaimsToDownstreamPathMiddleware();
        wenSocketsApp.UseLoadBalancingMiddleware();
        wenSocketsApp.UseDownstreamUrlCreatorMiddleware();
        wenSocketsApp.UseWebSocketsProxyMiddleware();
    });

await app.UseOcelot(config);
app.UseRouting();

app.Use(async (context, next) => { 
    await ApiGateway.Middlewares.CustomerCreationMiddleWare.InvokeAsync(context, next);
    await ApiGateway.Middlewares.CustomerInvitationMiddleWare.InvokeAsync(context, next);
});

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});
app.Run();

public partial class Program { }