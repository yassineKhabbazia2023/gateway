using System.Net;
using System.Text;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseHandler(IMockResponseRepository responseRepository) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri == null) return base.SendAsync(request, cancellationToken);
        var routeKey = $"{request.Method}:{request.RequestUri.AbsolutePath}".ToLower();
        var (success, fullPathFile) = responseRepository.GetResponseFullPathFile(routeKey);
        if (!success)
            return base.SendAsync(request, cancellationToken);
        var jsonResponse = File.ReadAllText(fullPathFile);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

