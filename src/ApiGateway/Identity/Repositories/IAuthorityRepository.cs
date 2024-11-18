using ApiGateway.Identity.Models;

namespace ApiGateway.Identity.Repositories
{
    public interface IAuthorityRepository
    {
        Task<IReadOnlyList<AuthorityJson>> FindAuthorities(bool? enabled = true);
    }
}
