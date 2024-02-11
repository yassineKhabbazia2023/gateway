using ApiGateway.DelegatingHandlers.Mocks;

namespace ApiGateway.UnitTests;

public class FileWatcherServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _tempFile;
    private bool _onChangeCalled;

    public FileWatcherServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _tempFile = Path.Combine(_tempDirectory, "tempfile.json");
        File.WriteAllText(_tempFile, "initial content");
    }

    [Fact]
    public void StartWatching_WhenFileChanges_OnChangeIsInvoked()
    {
        // Arrange
        _onChangeCalled = false;
        var service = new FileWatcherService();
        void OnChange() => _onChangeCalled = true;

        // Act
        service.StartWatching(_tempFile, OnChange);
        File.WriteAllText(_tempFile, "modified content");
        Thread.Sleep(500);

        // Assert
        Assert.True(_onChangeCalled);

        // Cleanup
        service.Dispose();
    }

    public void Dispose()
    {
        // Cleanup temporary directory and file
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}