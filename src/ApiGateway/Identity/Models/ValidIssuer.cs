using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Identity.Models
{
    [ExcludeFromCodeCoverage]
    public class ValidIssuer
    {
        public ValidIssuer(Guid id, string name, string roleName, bool canBeSystemAccount)
        {
            this.Id = id;
            this.Name = name;
            this.RoleName = roleName;
            this.CanBeSystemAccount = canBeSystemAccount;
        }

        public Guid Id { get; }

        public string Name { get; }

        public string RoleName { get; }

        public bool CanBeSystemAccount { get; }
    }
}
