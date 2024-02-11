
using ApiGateway.Extensions;
using Ocelot.Middleware;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiGatewayServices(builder.Configuration);
builder.Configuration.AddJsonConfiguration();
var app = builder.Build(); 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("healthz");
app.UseOcelot();
app.Run();