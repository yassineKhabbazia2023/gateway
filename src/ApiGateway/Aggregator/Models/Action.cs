using System.Text.Json.Serialization;

namespace ApiGateway.Aggregator.Models
{
    public class Action
    {
        public int? ActionId { get; set; }

        [JsonIgnore]
        public string? Name { get; set; }

        public string? Code { get; set; }

        public string? Label { get; set; }

        public bool Enabled { get; set; } = false;
    }
}
