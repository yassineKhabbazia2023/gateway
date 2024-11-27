using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Models
{
    [ExcludeFromCodeCoverage]
    public class AuthorityJson
    {
        public AuthorityJson(
            Guid id,
            string name,
            string url,
            bool validateIssuer,
            string jsonWebKeyFetchUrl,
            string nameClaimType,
            IReadOnlyList<SigningKey> signingKeys,
            IReadOnlyList<ValidAudience> validAudiences,
            IReadOnlyList<ValidIssuer> validIssuers)
        {
            this.Id = id;
            this.Name = name;
            this.Url = url;
            this.ValidateIssuer = validateIssuer;
            this.JsonWebKeyFetchUrl = jsonWebKeyFetchUrl;
            this.NameClaimType = nameClaimType;
            this.SigningKeys = new ReadOnlyCollection<SigningKey>(signingKeys.ToArray());
            this.ValidAudiences = new ReadOnlyCollection<ValidAudience>(validAudiences.ToArray());
            this.ValidIssuers = new ReadOnlyCollection<ValidIssuer>(validIssuers.ToArray());
        }

        [JsonProperty("id")]
        public Guid Id { get; }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("url")]
        public string Url { get; }

        [JsonProperty("validateIssuer")]
        public bool ValidateIssuer { get; }

        [JsonProperty("jsonWebKeyFetchUrl")]
        public string JsonWebKeyFetchUrl { get; }

        [JsonProperty("nameClaimType")]
        public string NameClaimType { get; }

        [JsonProperty("signingKeys")]
        public ReadOnlyCollection<SigningKey> SigningKeys { get; }

        [JsonProperty("validAudiences")]
        public ReadOnlyCollection<ValidAudience> ValidAudiences { get; }

        [JsonProperty("validIssuers")]
        public ReadOnlyCollection<ValidIssuer> ValidIssuers { get; }
    }
}
