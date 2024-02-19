using ApiGateway.Extensions;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Configuration.AddJsonConfiguration();
var app = builder.Build();
app.UseRouting();
app.UseSwagger(option => { option.RouteTemplate = "/gateway/api/{documentName}/api.json"; });

var assemblyName = typeof(Program).Assembly.GetName()
    .Name;

app.UseSwaggerUI(c =>
{
    c.EnableTryItOutByDefault();
    c.SwaggerEndpoint("/gateway/api/v1/api.json",
        $"{assemblyName} v1");
    c.RoutePrefix = "api";
});
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecks("health");
});

await app.UseOcelot();
app.Run();