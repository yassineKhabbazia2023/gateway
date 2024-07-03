namespace ApiGateway.Exceptions;

public class GatewayException : Exception
{
    public static string DefaultMessage => "There was an error related to the gateway itself.";

    public GatewayException()
        : base(DefaultMessage)
    {
    }


    public GatewayException(string message)
        : base(message)
    {
    }

    public GatewayException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
