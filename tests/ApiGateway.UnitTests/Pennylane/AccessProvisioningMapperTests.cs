using ApiGateway.Authorization.Models;
using ApiGateway.Pennylane.Mappers;
using ApiGateway.Pennylane.Models;
using FluentAssertions;

namespace ApiGateway.UnitTests.Pennylane;

public class AccessProvisioningMapperTests
{
    [Fact]
    public void MapToProvisioningResult_ShouldMapAllFields()
    {
        var source = new GrantPennylaneAccessResult
        {
            Status = "status",
            Message = "message",
            ContactId = 1,
            AccountId = 2,
            PennylaneUserId = "u",
            PennylaneCompanyId = "c",
            Role = "role",
            Error = "err"
        };

        var result = AccessProvisioningMapper.MapToProvisioningResult(source);

        result.Status.Should().Be(source.Status);
        result.Message.Should().Be(source.Message);
        result.ContactId.Should().Be(source.ContactId);
        result.AccountId.Should().Be(source.AccountId);
        result.ExternalUserId.Should().Be(source.PennylaneUserId);
        result.ExternalCompanyId.Should().Be(source.PennylaneCompanyId);
        result.Role.Should().Be(source.Role);
        result.Error.Should().Be(source.Error);
    }
}
