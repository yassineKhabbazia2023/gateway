
using ApiGateway.Extensions;
using Ocelot.Middleware;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Configuration.AddJsonConfiguration();
var app = builder.Build(); 
app.UseRouting();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecks("health");
});

await app.UseOcelot();
app.Run();