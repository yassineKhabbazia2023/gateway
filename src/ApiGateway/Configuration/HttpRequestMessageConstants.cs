namespace ApiGateway.Configuration;

public static class HttpRequestMessageConstants
{
    public static readonly string CurrentUserUriFragment = "currentuser";
    public static readonly string EmailUriFragment = "%7BdeductedCurrentUserEmail%7D"; // corresponds to "{deductedCurrentUserEmail}" in ocelot.json

    // If DownloadStreamUri contains DownloadStreamUriFragment
    // And accountId is set as a query params in UpstreamUrl
    // Then the accountNumber is deducted from given accountId
    public static readonly string DownloadStreamUriFragment = "%7BdeductedAccountNumber%7D"; // corresponds to "{deductedAccountNumber}" in ocelot.json
}
