using ApiGateway.Exceptions;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.Middlewares;

public class GatewayExceptionMiddleware
{
    private readonly ILogger<GatewayExceptionMiddleware> _logger;

    public GatewayExceptionMiddleware(ILogger<GatewayExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        try
        {
            await next.Invoke();
        }
        catch (GatewayException ex)
        {
            if (!context.Response.HasStarted)
            {
                _logger.LogError(ex, "Gateway exception {ErrorCode}: {ErrorMessage} (StatusCode: {StatusCode})",
                    ex.Code, ex.Message, ex.StatusCode);

                context.Response.Clear();
                context.Response.StatusCode = ex.StatusCode;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    ErrorCode = ex.Code,
                    ErrorMessage = ex.Message
                });
            }
        }
        catch (Exception ex)
        {
            if (!context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unexpected exception {ErrorCode}: {ErrorMessage} (StatusCode: 500)",
                    Errors.UnexpectedExceptionCode, ex.Message);

                context.Response.Clear();
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    ErrorCode = Errors.UnexpectedExceptionCode,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
