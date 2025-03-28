using Pulse.Back.ExceptionMiddleware.BaseException;

namespace ApiGateway.Exceptions;

public class GatewayException : BusinessException
{

    public int StatusCode { get; private set; }
    public GatewayException(int statusCode, string errorCode, string errorMessage)
        : base(errorCode, errorMessage)
    {
        this.StatusCode = statusCode;
    }
}


