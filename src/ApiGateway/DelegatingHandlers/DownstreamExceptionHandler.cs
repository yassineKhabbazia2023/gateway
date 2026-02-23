using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Pulse.ExceptionMiddleware.Model;
using System.Net.Http.Headers;
using System.Text;

namespace ApiGateway.DelegatingHandlers
{
    public class DownstreamExceptionHandler : DelegatingHandler
    {
        private readonly ILogger<DownstreamExceptionHandler> _logger;
        private readonly TelemetryClient _telemetryClient;

        public DownstreamExceptionHandler(ILogger<DownstreamExceptionHandler> logger, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
        }
        
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            StringBuilder builder = new StringBuilder();
            // request information
            string requestUrl = request.RequestUri is null ? string.Empty : request.RequestUri.ToString();
            string requestbody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            var telemetryContent = new Dictionary<string, string>();

            telemetryContent = FormatHeaders(telemetryContent, request.Headers);

            var httpResponse = await base.SendAsync(request, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                //response information
                int statusCode = (int)httpResponse.StatusCode;
                string content = await httpResponse.Content.ReadAsStringAsync();

                telemetryContent.TryAdd("Downstream Url", requestUrl);
                telemetryContent.TryAdd("Request Content", requestbody);
                telemetryContent.TryAdd("Response Status", statusCode.ToString());
                ErrorResponse? errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(content);
                if (errorResponse is null)
                {
                    telemetryContent.TryAdd("ErrorResponse", content);
                }
                else
                {
                    telemetryContent.TryAdd("ErrorCode", errorResponse.ErrorCode);
                    telemetryContent.TryAdd("ErrorMessage", errorResponse.ErrorMessage);
                }

                foreach (var keyvalue in telemetryContent)
                {
                    builder.AppendLine($"{keyvalue.Key}: {keyvalue.Value}");
                }

                _logger.LogError(builder.ToString());
                _telemetryClient.TrackEvent("Downstream Exceptions", telemetryContent);
            }
            return httpResponse;
        }

        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie"
        };

        private Dictionary<string, string> FormatHeaders(Dictionary<string, string> content, HttpHeaders headers)
        {
            if (headers == null || !headers.Any())
            {
                return content;
            }

            foreach (var header in headers)
            {
                if (SensitiveHeaders.Contains(header.Key))
                {
                    continue;
                }

                string headerValues = string.Join(", ", header.Value);
                content.TryAdd(header.Key, headerValues);
            }

            return content;
        }

    }
}
