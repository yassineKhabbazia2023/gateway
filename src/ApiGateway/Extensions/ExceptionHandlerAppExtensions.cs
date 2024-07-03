using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static System.Net.Mime.MediaTypeNames;

namespace ApiGateway.Extensions;

[ExcludeFromCodeCoverage]
public static class ExceptionHandlerAppExtensions
{
    public static void CatchExceptions(this IApplicationBuilder app)
    {
        app.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // using static System.Net.Mime.MediaTypeNames;
            context.Response.ContentType = Text.Plain;

            await context.Response.WriteAsync("An exception was thrown while handling this request and was not handled by the gateway.");

            var exceptionHandlerPathFeature =
                context.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionHandlerPathFeature?.Path != null)
            {
                await context.Response.WriteAsync($" Path: {exceptionHandlerPathFeature?.Path}.");
            }

            if (exceptionHandlerPathFeature?.Error != null)
            {
                await context.Response.WriteAsync($" Error: {exceptionHandlerPathFeature?.Error.GetType().Name}.");
                await context.Response.WriteAsync($" Message: {exceptionHandlerPathFeature?.Error.Message}.");
                await context.Response.WriteAsync($" Inner: {exceptionHandlerPathFeature?.Error.InnerException?.GetType().Name}.");
                await context.Response.WriteAsync($" Inner Message: {exceptionHandlerPathFeature?.Error.InnerException?.Message}.");
            }
        });
    }
}
