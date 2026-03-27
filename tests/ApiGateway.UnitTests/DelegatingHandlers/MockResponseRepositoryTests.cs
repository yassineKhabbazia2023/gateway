using ApiGateway.DelegatingHandlers.Mocks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class MockResponseRepositoryTests
{
    private readonly Mock<BlobContainerClient> _containerClientMock = new();
    private readonly Mock<BlobClient> _blobClientMock = new();

    public MockResponseRepositoryTests()
    {
        _containerClientMock
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(_blobClientMock.Object);
    }

    private MockResponseRepository CreateRepository() => new(_containerClientMock.Object);

    // ---- ToSafeBlobName / FromBlobName ----

    [Theory]
    [InlineData("get:/api/contacts/list", "get--__api__contacts__list.json")]
    [InlineData("post:/api/users", "post--__api__users.json")]
    public void ToSafeBlobName_EncodesRouteKeyCorrectly(string routeKey, string expectedBlobName)
    {
        var blobName = MockResponseRepository.ToSafeBlobName(routeKey);
        blobName.Should().Be(expectedBlobName);
    }

    [Theory]
    [InlineData("get--__api__contacts__list.json", "get:/api/contacts/list")]
    [InlineData("post--__api__users.json", "post:/api/users")]
    public void FromBlobName_DecodesCorrectly(string blobName, string expectedRouteKey)
    {
        var routeKey = MockResponseRepository.FromBlobName(blobName);
        routeKey.Should().Be(expectedRouteKey);
    }

    [Fact]
    public void ToSafeBlobName_AndFromBlobName_AreReversible()
    {
        var originalKey = "get:/api/contacts/search?id=123";
        var blobName = MockResponseRepository.ToSafeBlobName(originalKey);
        var decoded = MockResponseRepository.FromBlobName(blobName);
        decoded.Should().Be(originalKey);
    }

    // ---- GetJsonContentAsync ----

    [Fact]
    public async Task GetJsonContentAsync_BlobExists_ReturnsTrueAndContent()
    {
        // Arrange
        var expectedJson = "{\"name\":\"test\"}";
        var downloadResult = BlobsModelFactory.BlobDownloadResult(
            content: BinaryData.FromString(expectedJson));
        _blobClientMock
            .Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(downloadResult, Mock.Of<Response>()));

        var repository = CreateRepository();

        // Act
        var (success, jsonContent) = await repository.GetJsonContentAsync("get:/api/test");

        // Assert
        success.Should().BeTrue();
        jsonContent.Should().Be(expectedJson);
    }

    [Fact]
    public async Task GetJsonContentAsync_BlobNotFound_ReturnsFalseAndNull()
    {
        // Arrange
        _blobClientMock
            .Setup(b => b.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var repository = CreateRepository();

        // Act
        var (success, jsonContent) = await repository.GetJsonContentAsync("get:/api/unknown");

        // Assert
        success.Should().BeFalse();
        jsonContent.Should().BeNull();
    }

    // ---- UpsertAsync ----

    [Fact]
    public async Task UpsertAsync_UploadsBlob()
    {
        // Arrange
        _blobClientMock
            .Setup(b => b.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        var repository = CreateRepository();

        // Act
        await repository.UpsertAsync("post:/api/users", "{\"id\":1}");

        // Assert
        _blobClientMock.Verify(
            b => b.UploadAsync(It.IsAny<BinaryData>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task DeleteAsync_BlobExists_ReturnsTrue()
    {
        // Arrange
        _blobClientMock
            .Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var repository = CreateRepository();

        // Act
        var result = await repository.DeleteAsync("get:/api/test");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_BlobNotFound_ReturnsFalse()
    {
        // Arrange
        _blobClientMock
            .Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var repository = CreateRepository();

        // Act
        var result = await repository.DeleteAsync("get:/api/unknown");

        // Assert
        result.Should().BeFalse();
    }
}
