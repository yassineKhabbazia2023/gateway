using ApiGateway.Configuration;
using ApiGateway.Contact;
using ApiGateway.Extensions;
using ApiGateway.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.FeatureManagement;
using Ocelot.Authorization.Middleware;
using Ocelot.Middleware;
using Ocelot.Values;
using static System.Net.Mime.MediaTypeNames;

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

if (app.Environment.IsDevelopment())
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
    endpoints.MapControllers();
    endpoints.MapHealthChecks("health");
});

app.UseSwagger();

var config = new OcelotPipelineConfiguration
{
    AuthorizationMiddleware
                = async (downStreamContext, next) =>
                await ApiGateway.Middlewares.AuthorizationMiddleware.AuthorizationFilter(downStreamContext, next)
};

await app.UseOcelot(config);
app.Run();