using System.Collections.Specialized;
using System.Web;
using ApiGateway.Account;
using ApiGateway.Configuration;

namespace ApiGateway.Extensions;

public static class HttpRequestMessageExtensions
{
    public static bool UriContainsFragment(this HttpRequestMessage request, string fragment)
    {
        var uriPath = request.RequestUri!.AbsolutePath;
        return uriPath.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }

    public static void ModifyRequestUri(this HttpRequestMessage request, string paramName, string paramValue, string fragmentToRemove)
    {
        var uriBuilder = new UriBuilder(request.RequestUri!);
        var segments = uriBuilder.Path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        segments.Remove(fragmentToRemove);
        uriBuilder.Path = String.Join("/", segments);

        if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramValue))
        {
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[paramName] = paramValue;
            uriBuilder.Query = query.ToString();
        }

        request.RequestUri = uriBuilder.Uri;
    }

    public static void PrepareRequestHeader(
        this HttpRequestMessage request,
        string contactEmail,
        string? contactId
      )
    {
        if (request == null)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(contactId))
        {
            request.Headers.Add("CurrentUser", contactId);
            request.Headers.Add("ContactEmail", contactEmail);

            if (request.UriContainsFragment($"/{HttpRequestMessageConstants.CurrentUserUriFragment}"))
            {
                request.ModifyRequestUri(nameof(contactId), contactId!, HttpRequestMessageConstants.CurrentUserUriFragment);
            }
            if (request.UriContainsFragment(HttpRequestMessageConstants.EmailUriFragment))
            {
                request.ModifyRequestUri("email", contactEmail!, HttpRequestMessageConstants.EmailUriFragment);
            }
        }
    }
}
