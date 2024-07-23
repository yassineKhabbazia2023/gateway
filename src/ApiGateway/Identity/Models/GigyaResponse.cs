using System.Text.Json.Serialization;

namespace ApiGateway.Identity.Models
{
    public class GigyaResponse
    {
        [JsonPropertyName("errorDetails")]
        public string? ErrorDetails { get; set; }

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }
}
