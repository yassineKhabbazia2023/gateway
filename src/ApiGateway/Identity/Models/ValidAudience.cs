namespace ApiGateway.Identity.Models
{
    public class ValidAudience
    {
        public ValidAudience(Guid id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        public Guid Id { get; }

        public string Name { get; }
    }
}
