using System.Net;
using System.Text;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseHandler(
    IMockResponseRepository responseRepository) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
            return base.SendAsync(request,
                cancellationToken);

        var pathAndQuery = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri.PathAndQuery
            : request.RequestUri.OriginalString;
        var routeKey = $"{request.Method}:{pathAndQuery}".ToLower();

        var (success, jsonContent) = responseRepository.GetJsonContent(routeKey);
        if (!success)
            return base.SendAsync(request,
                cancellationToken);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonContent!,
                Encoding.UTF8,
                "application/json")
        };
        return Task.FromResult(response);
    }

}