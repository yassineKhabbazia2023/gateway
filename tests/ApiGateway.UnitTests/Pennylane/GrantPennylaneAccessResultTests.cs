using ApiGateway.Pennylane.Models;

namespace ApiGateway.UnitTests.Pennylane;

public class GrantPennylaneAccessResultTests
{
    [Fact]
    public void WithError_ShouldPopulateErrorAndMessage()
    {
        // Arrange
        var result = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Created,
            Message = "ok",
            AccountId = 1,
            ContactId = 2
        };

        // Act
        var returned = result.WithError("ERR", "msg");

        // Assert
        returned.Should().BeSameAs(result);
        result.Error.Should().Be("ERR");
        result.Message.Should().Be("msg");
    }
}
