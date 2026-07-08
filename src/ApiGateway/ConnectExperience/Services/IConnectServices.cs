using ApiGateway.ConnectExperience.Models;
using ApiGateway.Models;

namespace ApiGateway.ConnectExperience.Services
{
    public interface IConnectServices
    {
        Task<UserInformation> GetUserInformation(string userEmail);

        Task<Summary?> GetSummaryAsync(int accountId, int currentUserId, string contactType);

        Task<IEnumerable<int>> SendEmailAsync(string userEmail, int accountId, int[] customerIDs, string? entityType = null);
    }
}
