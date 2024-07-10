using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;
using Ocelot.Middleware;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Claims;

namespace ApiGateway.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var upstream = context.Request.Path;
        var downstream = context.Items.DownstreamRoute();
        var downstreamRequest = context.Items.DownstreamRequest();
        var claims = AuthorizationMiddleware.ValidateRequireClaim(context);
        var userEmail = AuthorizationMiddleware.ValidateUserIdentity(context);

        try
        {
            await _next.Invoke(context);
        }
        catch(GatewayException ex)
        {
            LogErrorWithPrefix("Gateway", upstream, claims, downstream.DownstreamPathTemplate?.Value ?? "", userEmail, ex);
        }
        catch (Exception ex)
        {
            var downStreamResponse = context.Items.DownstreamResponse();
            if(downStreamResponse != null)
            {
                LogDownstreamResponseError(downStreamResponse, downstreamRequest.AbsolutePath);
            }

            LogErrorWithPrefix("Downstream", upstream, claims, downstream.DownstreamPathTemplate?.Value ?? "", userEmail, ex);
        }
    }

    [ExcludeFromCodeCoverage]
    private void LogErrorWithPrefix(string prefix, string upstream, IEnumerable<string> claims, string downstream, string userEmail, Exception ex)
    {
        _logger.LogError($@"[{prefix}] >> There was an error while executing the request for the following upstream path: {upstream}.
                                Downstream: {downstream}, claims: {string.Join(',', claims)}, user email: {userEmail}.
                                The error was: {ex.GetType().Name} with the following message: {ex.Message}.
                                {(ex.InnerException != null ? "Inner Exception: " + ex.InnerException.Message : "")}");
    }

    [ExcludeFromCodeCoverage]
    private void LogDownstreamResponseError(DownstreamResponse response, string path)
    {
        _logger.LogError($@"[DownstreamResponse] >> There was an error while executing the request for the following downstream path: {path}.
                            Status Code: {response.StatusCode}, Reason Phrase: {response.ReasonPhrase}");
    }
}
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionMiddleware>();
    }
}

