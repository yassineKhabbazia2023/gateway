using System.IO.Abstractions;
using System.Text.Json;
using ApiGateway.DelegatingHandlers.Mocks;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.UnitTests;

public class MockResponseRepositoryTests
{
    private readonly Mock<IFileSystem> _mockFileSystem = new();
    private readonly Mock<IFileWatcherService> _mockFileWatcherService = new();
    private readonly IConfiguration _configuration;

    public MockResponseRepositoryTests()
    {
        var inMemorySettings = new Dictionary<string, string> {
            {"MOCK_REPOSITORY_PATH", "mocks"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        // Setup file system mock with correct existence and read text behaviors
        _mockFileSystem.Setup(fs => fs.File.Exists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.File.ReadAllText(It.IsAny<string>())).Returns("[]");
    }

    [Fact]
    public void GetResponseFullPathFile_WithNonExistingRouteKey_ReturnsFailure()
    {
        // Arrange
        var repository = new MockResponseRepository(_configuration, _mockFileSystem.Object, _mockFileWatcherService.Object);

        // Act
        var (success, fullPathFile) = repository.GetResponseFullPathFile("non-existing-route");

        // Assert
        success.Should().BeFalse();
        fullPathFile.Should().BeNull();
    }

    [Fact]
    public void GetResponseFullPathFile_WithExistingRouteKey_ReturnsSuccessAndPath()
    {
        // Arrange
        var mockIndexContent = JsonSerializer.Serialize(new List<MockedRouteConfig>
        {
            new( "GET", "/test", "response.json" )
        });

        _mockFileSystem.Setup(fs => fs.File.ReadAllText(It.IsAny<string>())).Returns(mockIndexContent);
        var expectedFilePath = "mocks\\response.json";
        _mockFileSystem.Setup(fs => fs.File.Exists(expectedFilePath)).Returns(true);

        var repository = new MockResponseRepository(_configuration, _mockFileSystem.Object, _mockFileWatcherService.Object);

        // Act
        var (success, fullPathFile) = repository.GetResponseFullPathFile("get:/test");

        // Assert
        success.Should().BeTrue();
        fullPathFile.Should().Be(expectedFilePath);
    }
}