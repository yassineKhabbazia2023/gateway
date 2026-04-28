using Newtonsoft.Json;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.DelegatingHandlers
{
    public sealed class DownstreamExceptionHandler : DelegatingHandler
    {
        private readonly ILogger<DownstreamExceptionHandler> _logger;

        public DownstreamExceptionHandler(ILogger<DownstreamExceptionHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string requestUrl = request.RequestUri is null ? string.Empty : request.RequestUri.ToString();

            var httpResponse = await base.SendAsync(request, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                int statusCode = (int)httpResponse.StatusCode;
                string content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                ErrorResponse? errorResponse = null;
                try
                {
                    errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(content);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize downstream error response from {DownstreamUrl} - Status: {ResponseStatus}, RawContent: {RawContent}",
                        requestUrl, statusCode, content);
                }

                if (statusCode >= 500)
                {
                    _logger.LogError("Downstream exception from {DownstreamUrl} - Status: {ResponseStatus}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        requestUrl,
                        statusCode,
                        errorResponse?.ErrorCode ?? "N/A",
                        errorResponse?.ErrorMessage ?? content);
                }
                else
                {
                    _logger.LogWarning("Downstream exception from {DownstreamUrl} - Status: {ResponseStatus}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                        requestUrl,
                        statusCode,
                        errorResponse?.ErrorCode ?? "N/A",
                        errorResponse?.ErrorMessage ?? content);
                }
            }
            return httpResponse;
        }
    }
}
