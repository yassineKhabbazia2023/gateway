using ApiGateway.Extensions;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddDistributedMemoryCache();
builder.Configuration.AddJsonConfiguration();
var app = builder.Build();
app.UseRouting();

app.UseSwaggerForOcelotUI(opt =>
{
    opt.DownstreamSwaggerEndPointBasePath = "/swagger/docs";
}, c =>
{
    c.EnableTryItOutByDefault();
    c.RoutePrefix = "api";
});

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