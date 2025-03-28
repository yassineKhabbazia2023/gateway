using ApiGateway.Exceptions;
using Microsoft.ApplicationInsights;

namespace ApiGateway.Middlewares;

public static class GatewayExceptionMiddleware
{
    public static Func<HttpContext, Func<Task>, Task> ExceptionFilter => async (context, next) =>
    {
        try
        {
            await next.Invoke();
        }
        catch (GatewayException ex)
        {
            if (!context.Response.HasStarted)
            {
                var telemetry = context.RequestServices.GetRequiredService<TelemetryClient>();
                Dictionary<string, string> telemetryContent = new Dictionary<string, string>();
                telemetryContent["StatusCode"] = ex.StatusCode.ToString();
                telemetryContent["ErrorCode"] = ex.Code;
                telemetryContent["ErrorMessage"] = ex.Message;
                telemetry.TrackException(ex, telemetryContent);

                context.Response.Clear();
                context.Response.StatusCode = ex.StatusCode;
                await context.Response.WriteAsJsonAsync(new Pulse.ExceptionMiddleware.Model.ErrorResponse() { ErrorCode = ex.Code, ErrorMessage = ex.Message });
            }
        }
        catch (Exception ex)
        {
            if (!context.Response.HasStarted)
            {
                var telemetry = context.RequestServices.GetRequiredService<TelemetryClient>();
                Dictionary<string, string> telemetryContent = new Dictionary<string, string>();
                telemetryContent["StatusCode"] = "500";
                telemetryContent["ErrorCode"] = Errors.UnexpectedExceptionCode;
                telemetryContent["ErrorMessage"] = ex.Message;
                telemetry.TrackException(ex, telemetryContent);

                context.Response.Clear();
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new Pulse.ExceptionMiddleware.Model.ErrorResponse() { ErrorCode = Errors.UnexpectedExceptionCode, ErrorMessage = ex.Message });
            }
        }
    };
}



