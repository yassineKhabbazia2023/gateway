using ApiGateway.Configuration;
using ApiGateway.Contact;
using ApiGateway.Extensions;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();

builder.Configuration.AddJsonConfiguration();

var app = builder.Build();
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
}

app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecks("health");
});

app.UseSwagger();
await app.UseOcelot();
app.Run();