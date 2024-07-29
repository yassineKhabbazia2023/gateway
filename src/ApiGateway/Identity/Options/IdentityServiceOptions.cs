using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Identity.Options
{
    public class IdentityServiceOptions
    {
        public required string GigyaApiKey { get; set; }

        public required string GigyaSecret { get; set; }

        public required string GigyaUserKey { get; set; }

        public required string CollaboratorsSecurityGroup { get; set; }

        public required string CollaboratorRole { get; set; }

        public required string CustomerRole { get; set; }
    }
}
