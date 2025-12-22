using ApiGateway.Authorization.Models;
using ApiGateway.Pennylane.Models;

namespace ApiGateway.Pennylane.Mappers
{
    public static class AccessProvisioningMapper
    {
        public static AccessProvisioningResult MapToProvisioningResult(GrantPennylaneAccessResult result)
        {
            return new AccessProvisioningResult
            {
                Status = result.Status,
                Message = result.Message,
                ContactId = result.ContactId,
                AccountId = result.AccountId,
                ExternalUserId = result.PennylaneUserId,
                ExternalCompanyId = result.PennylaneCompanyId,
                Role = result.Role,
                Error = result.Error
            };
        }
    }
}
