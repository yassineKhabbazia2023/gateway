using System.Text.Json.Serialization;

namespace ApiGateway.Offer.Model
{
    public class CreateSubscriptionOffer
    {
        public int AccountId { get; set; }

        public int OfferId { get; set; }

        public int? PlanId { get; set; }

        public IReadOnlyList<int> ProductConfigurations { get; set; } = [];

        public IReadOnlyList<int> Contacts { get; set; } = [];

        public IReadOnlyList<Note> Notes { get; set; } = [];

        public string? Applicant { get; set; }

        public string? Status { get; set; }

        public IReadOnlyList<ContactFunctions>? ContactFunctions { get; set; }

        [JsonIgnore]
        public DateTime? CreationDate { get; set; }
    }

    public class Note
    {
        public string? StepName { get; set; }

        public string? StepNote { get; set; }
    }

    public class ContactFunctions
    {
        public int ContactId { get; set; }

        public int FunctionId { get; set; }
    }
}
