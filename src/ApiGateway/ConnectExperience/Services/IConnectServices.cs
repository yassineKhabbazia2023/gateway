using ApiGateway.ConnectExperience.Models;

namespace ApiGateway.ConnectExperience.Services
{
    public interface IConnectServices
    {
        Task<UserInformation> GetUserInformation(string userEmail);
    }
}
