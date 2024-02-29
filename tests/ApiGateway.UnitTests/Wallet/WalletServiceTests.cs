using ApiGateway.Wallet;
using ApiGateway.Wallet.Models;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Wallet;

public class WalletServiceTests
{
    private readonly Mock<ILogger<WalletService>> _loggerMock;
    private readonly WalletService _service;

    public WalletServiceTests()
    {
        _loggerMock = new();
        _service = new(new(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetResponsesAsync_IsCalled_ReturnFiveWalletResponses()
    {
        //Arrange
        // Act
        var responses = await _service.GetResponsesAsync();

        // Assert
        responses.Should()
            .HaveCount(5);
        responses.Should()
            .AllBeOfType<WalletResponse>();

    }
    
}