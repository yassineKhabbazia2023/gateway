namespace ApiGateway.Identity.Models
{
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
