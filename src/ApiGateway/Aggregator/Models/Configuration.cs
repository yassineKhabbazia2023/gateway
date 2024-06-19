using System.Text.Json.Serialization;

namespace ApiGateway.Aggregator.Models
{
    public class Configuration
    {
        [JsonPropertyName("category")]
        public required string Category { get; set; }

        [JsonPropertyName("actions")]
        public required IEnumerable<Action> Actions { get; set; }
    }
}
