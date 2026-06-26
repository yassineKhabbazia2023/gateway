using System.Text.RegularExpressions;
using System.Web;
using ApiGateway.Account;
using ApiGateway.Configuration;
using ApiGateway.Exceptions;

namespace ApiGateway.Extensions;

public static class HttpRequestMessageExtensions
{
    private static readonly char[] PathSeparator = { '/' };

    public static bool UriContainsFragment(this HttpRequestMessage request, string fragment)
    {
        var uriPath = request.RequestUri!.AbsolutePath;
        return uriPath.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }

    public static void ModifyRequestUri(this HttpRequestMessage request, string paramName, string paramValue, string fragmentToRemove)
    {
        if (request is null)
        {
            return;
        }

        var uriBuilder = new UriBuilder(request.RequestUri!);
        var segments = uriBuilder.Path.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        segments.Remove(fragmentToRemove);
        uriBuilder.Path = String.Join("/", segments);

        if (!string.IsNullOrEmpty(paramName)
            && !string.IsNullOrEmpty(paramValue))
        {
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[paramName] = paramValue;
            uriBuilder.Query = query.ToString();
        }

        request.RequestUri = uriBuilder.Uri;
    }

    public static async Task PrepareRequestHeader(
        this HttpRequestMessage request, 
        string contactEmail, 
        string? contactId,
        string? contactType,
        IAccountService accountService
      )
    {
        if (request is null)
        {
            throw new GatewayException(StatusCodes.Status406NotAcceptable, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(request)));
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(contactId))
        {
            request.Headers.Add("CurrentUser", contactId);
            request.Headers.Add("ContactEmail", contactEmail);
            request.Headers.Add("ContactType", contactType?.ToString());

            if (request.UriContainsFragment($"/{HttpRequestMessageConstants.CurrentUserUriFragment}"))
            {
                request.ModifyRequestUri(nameof(contactId), contactId!, HttpRequestMessageConstants.CurrentUserUriFragment);
            }
            if (request.UriContainsFragment(HttpRequestMessageConstants.EmailUriFragment))
            {
                request.ModifyRequestUri("email", contactEmail!, HttpRequestMessageConstants.EmailUriFragment);
            }
        }

        // Pour les routes qui contiennent le fragment DownloadStreamUriFragment côte Api
        if (request.UriContainsFragment(HttpRequestMessageConstants.DownloadStreamUriFragment))
        {
            if (!int.TryParse(request.GetQueryParam("accountId"), out int accountId))
            {
                throw new GatewayException(StatusCodes.Status404NotFound, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(accountId)));
            }

            if(accountService is null)
            {
                throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullConfigurationCode, string.Format(Errors.NullConfigurationMessage, nameof(accountService)));
            }

            var relatedAccount = await accountService!.GetAccountAsync(accountId);
            var deductedAccountNumber = relatedAccount?.AccountNumber;
            request.ReplaceInRequestUri(HttpRequestMessageConstants.DownloadStreamUriFragment, deductedAccountNumber ?? String.Empty);
        }
    }

    public static void ReplaceInRequestUri(this HttpRequestMessage request, string fragmentToReplace, string newValue)
    {
        if (request == null)
        {
            return;
        }

        var uriBuilder = new UriBuilder(request.RequestUri!);
        var newPath = uriBuilder.Path.Replace(fragmentToReplace, newValue);
        uriBuilder.Path = newPath;

        request.RequestUri = uriBuilder.Uri;
    }

    public static string? GetQueryParam(this HttpRequestMessage request, string queryParamName)
    {
        var query = request.RequestUri?.Query ?? string.Empty;
        return ExtractQueryParam(queryParamName, query) ?? String.Empty;
    }

    static string? ExtractQueryParam(string queryParamName, string query)
    {
        var pattern = $"{queryParamName}=(?<paramValue>\\w+)";
        Match match = Regex.Match(query, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        var paramValue = match.Groups["paramValue"]?.Value;

        return paramValue;
    }
}
