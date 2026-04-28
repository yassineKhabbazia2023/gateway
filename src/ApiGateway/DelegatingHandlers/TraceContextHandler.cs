using System.Diagnostics;

namespace ApiGateway.DelegatingHandlers;

public sealed class TraceContextHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return base.SendAsync(request, cancellationToken);
        }

        if (!request.Headers.Contains("traceparent"))
        {
            request.Headers.TryAddWithoutValidation("traceparent", activity.Id);
        }

        if (!string.IsNullOrEmpty(activity.TraceStateString)
            && !request.Headers.Contains("tracestate"))
        {
            request.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
