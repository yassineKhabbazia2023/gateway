using ApiGateway.Extensions;

namespace ApiGateway.UnitTests.Extensions;

public class TempFileHelperTests
{
    [Fact]
    public void GetOcelotTempDir_WhenCalled_ReturnsValidPath()
    {
        // Act
        var path = TempFileHelper.GetOcelotTempDir();

        // Assert
        path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetOcelotTempDir_WhenCalled_ReturnsDirectoryThatExists()
    {
        // Act
        var path = TempFileHelper.GetOcelotTempDir();

        // Assert
        Directory.Exists(Path.GetDirectoryName(path)).Should().BeTrue();
    }

    [Fact]
    public void GetOcelotTempDir_CalledMultipleTimes_ReturnsSamePath()
    {
        // Act
        var path1 = TempFileHelper.GetOcelotTempDir();
        var path2 = TempFileHelper.GetOcelotTempDir();

        // Assert
        path1.Should().Be(path2);
    }
    // Assuming the above tests for GetOcelotTempDir are part of the same class

    [Fact]
    public void GetLiteDbTempDir_WhenCalled_ReturnsValidPath()
    {
        // Act
        var path = TempFileHelper.GetLiteDbTempDir();

        // Assert
        path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetLiteDbTempDir_WhenCalled_ReturnsDirectoryThatExists()
    {
        // Act
        var path = TempFileHelper.GetLiteDbTempDir();

        // Assert
        Directory.Exists(Path.GetDirectoryName(path)).Should().BeTrue();
    }

    [Fact]
    public void GetLiteDbTempDir_CalledMultipleTimes_ReturnsSamePath()
    {
        // Act
        var path1 = TempFileHelper.GetLiteDbTempDir();
        var path2 = TempFileHelper.GetLiteDbTempDir();

        // Assert
        path1.Should().Be(path2);
    }
}