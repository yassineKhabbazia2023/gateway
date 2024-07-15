using System.Collections.Specialized;
using System.Web;

namespace ApiGateway.Extensions;

public static class HttpRequestMessageExtensions
{
    public static bool ShouldSetContactId(this HttpRequestMessage request)
    {
        var uriPath = request.RequestUri!.AbsolutePath;
        return uriPath.Contains("/currentuser", StringComparison.OrdinalIgnoreCase);
    }

    public static void ModifyRequestUri(this HttpRequestMessage request, string paramName, string paramValue)
    {
        var uriBuilder = new UriBuilder(request.RequestUri!);
        var segments = uriBuilder.Path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        segments.Remove("currentuser");
        uriBuilder.Path = String.Join("/", segments);

        if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramValue))
        {
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[paramName] = paramValue;
            uriBuilder.Query = query.ToString();
        }

        request.RequestUri = uriBuilder.Uri;
    }
}
