using System.Net;
using System.Text;
using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.FeatureFlags;
using ApiGateway.Mocks;
using ApiGateway.Mocks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.UnitTests.Mocks;

public class MocksControllerTests
{
    private readonly Mock<IMockResponseRepository> _mockRepo = new();
    private readonly Mock<IFeatureFlagService> _featureFlagService = new(MockBehavior.Strict);

    private MocksController CreateController() => new(_mockRepo.Object, _featureFlagService.Object);

    // ---- GET /mocks/get-mock-index ----

    [Fact]
    public async Task GetMockIndex_WhenCalled_ReturnsOkResultWithMockEntries()
    {
        // Arrange
        var mockEntries = new List<MockEntry>
        {
            new("get:/api/contacts", "{\"name\":\"test\"}"),
            new("post:/api/users", "{}")
        };
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.ListAllAsync()).ReturnsAsync(mockEntries);

        var controller = CreateController();

        // Act
        var result = await controller.GetMockIndex();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult?.Value.Should().BeEquivalentTo(mockEntries);
    }

    [Fact]
    public async Task GetMockIndex_WhenFeatureGate_False_ReturnsForbidden()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController();

        // Act
        var result = await controller.GetMockIndex() as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    // ---- POST /mocks/modify-mock-response ----

    [Fact]
    public async Task ModifyMockResponse_FileIsNotJson_ReturnsBadRequest()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(_ => _.FileName).Returns("invalid.txt");

        var request = new MockIndexRequest { DownstreamUri = "http://test.com", HttpVerb = "GET" };
        var fileUpdate = new MockEntryFileUpdate { File = mockFile.Object };

        // Act
        var result = await controller.ModifyMockResponse(request, fileUpdate);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        (result as BadRequestObjectResult)?.Value.Should().Be("Only JSON files are accepted.");
    }

    [Fact]
    public async Task ModifyMockResponse_FileContentIsNotValidJson_ReturnsBadRequest()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("response.json");
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("Invalid JSON")));

        var request = new MockIndexRequest { DownstreamUri = "http://example.com", HttpVerb = "GET" };
        var fileUpdate = new MockEntryFileUpdate { File = mockFile.Object };

        // Act
        var result = await controller.ModifyMockResponse(request, fileUpdate);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        (result as BadRequestObjectResult)?.Value.Should().Be("The file content is not valid JSON.");
    }

    [Fact]
    public async Task ModifyMockResponse_ValidFile_CallsUpsertAndReturnsOk()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("valid.json");
        mockFile.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream("{}"u8.ToArray()));

        var request = new MockIndexRequest { DownstreamUri = "http://test.com", HttpVerb = "GET" };
        var fileUpdate = new MockEntryFileUpdate { File = mockFile.Object };

        // Act
        var result = await controller.ModifyMockResponse(request, fileUpdate);

        // Assert
        _mockRepo.Verify(r => r.UpsertAsync("get:http://test.com", "{}"), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().Be("Mock response updated successfully.");
    }

    [Fact]
    public async Task ModifyMockResponse_WhenFeatureGate_False_ReturnsForbidden()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController();

        var request = new MockIndexRequest { DownstreamUri = "http://example.com", HttpVerb = "GET" };
        var fileUpdate = new MockEntryFileUpdate { File = new Mock<IFormFile>().Object };

        // Act
        var result = await controller.ModifyMockResponse(request, fileUpdate) as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    // ---- DELETE /mocks/delete-mock-response ----

    [Fact]
    public async Task DeleteMockResponse_ExistingKey_ReturnsOk()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.DeleteAsync("get:/api/contacts")).ReturnsAsync(true);
        var controller = CreateController();

        // Act
        var result = await controller.DeleteMockResponse("get:/api/contacts");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().Be("Mock response deleted successfully.");
    }

    [Fact]
    public async Task DeleteMockResponse_NonExistingKey_ReturnsNotFound()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockRepo.Setup(r => r.DeleteAsync("get:/api/unknown")).ReturnsAsync(false);
        var controller = CreateController();

        // Act
        var result = await controller.DeleteMockResponse("get:/api/unknown");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteMockResponse_EmptyRouteKey_ReturnsBadRequest()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController();

        // Act
        var result = await controller.DeleteMockResponse("");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteMockResponse_WhenFeatureGate_False_ReturnsForbidden()
    {
        // Arrange
        _featureFlagService.Setup(f => f.IsEnabledAsync(FeatureFlagKeys.AreMocksEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController();

        // Act
        var result = await controller.DeleteMockResponse("get:/api/contacts") as StatusCodeResult;

        // Assert
        result!.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }
}
