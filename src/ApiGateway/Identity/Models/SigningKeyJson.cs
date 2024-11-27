using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Models
{
    [ExcludeFromCodeCoverage]
    public class SigningKey
    {
        public SigningKey(Guid id, string jsonWebKey)
        {
            this.Id = id;
            this.JsonWebKey = jsonWebKey;
        }

        public Guid Id { get; }

        public string JsonWebKey { get; }
    }
}
