using ApiGateway.Wallet;
using ApiGateway.Wallet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests;
/// <summary>
///This class is just for a template demo purpose and should fixed later S
/// </summary>
public class WalletDesktopControllerTests
{
    private readonly Mock<IWalletService> _mockWalletService;
    private readonly Mock<ILogger<WalletController>> _mockLoggerMock;
    private readonly WalletController _controller;

    public WalletDesktopControllerTests()
    {
        _mockWalletService = new();
        _mockLoggerMock = new();
        _controller = new(_mockWalletService.Object, _mockLoggerMock.Object);
    }

    [Fact]
    public async Task Get_Called_ReturnOkResultWithWalletResponses()
    {
        // Arrange
        var mockResponses = new List<WalletResponse>
        {
            new()
        };

        _mockWalletService.Setup(service => service.GetResponsesAsync()).ReturnsAsync(mockResponses);

        // Act
        var result = await _controller.Get();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(200); 
        var returnedResponses = okResult.Value as IEnumerable<WalletResponse>;
        returnedResponses.Should().BeEquivalentTo(mockResponses);
    }
}