using System.Text.Json.Serialization;

namespace ApiGateway.Aggregator.NotificationSettings.Models
{
    public record Setting
    {
        public int Id { get; set; }

        public string Label { get; set; }

        public bool? FeedcenterValue { get; set; }

        public bool? EmailValue { get; set; }
    }
}
