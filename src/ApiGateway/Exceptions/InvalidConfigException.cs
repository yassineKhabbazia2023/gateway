namespace ApiGateway.Exceptions;

public class InvalidConfigException : Exception
{
    public static string DefaultMessage => "The configuration provided is invalid.";

    public static string MissingConfigMessage(string configName)
    {
        return $"The configuration '{configName}' is required but was not found.";
    }


    public static string IncorrectValueMessage(string configName)
    {
        return $"The value provided for '{configName}' is incorrect or not supported.";
    }


    public InvalidConfigException()
        : base(DefaultMessage)
    {
    }


    public InvalidConfigException(string message)
        : base(message)
    {
    }

    public InvalidConfigException(string message, Exception inner)
        : base(message, inner)
    {
    }
}