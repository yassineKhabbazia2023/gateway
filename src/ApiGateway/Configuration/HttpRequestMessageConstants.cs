namespace ApiGateway.Configuration;

public static class HttpRequestMessageConstants
{
    public static readonly string CurrentUserUriFragment = "currentuser";
    public static readonly string EmailUriFragment = "%7BdeductedCurrentUserEmail%7D"; // corresponds to "{deductedCurrentUserEmail}" in ocelot.json
}