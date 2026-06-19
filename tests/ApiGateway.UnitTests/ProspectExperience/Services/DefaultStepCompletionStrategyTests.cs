using System.Net;
using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

/// <summary>
/// Unit tests for <see cref="DefaultStepCompletionStrategy"/>.
/// </summary>
public sealed class DefaultStepCompletionStrategyTests
{
    private readonly Mock<IProspectApiClient> _prospectClient = new();
    private readonly DefaultStepCompletionStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStepCompletionStrategyTests"/> class.
    /// </summary>
    public DefaultStepCompletionStrategyTests()
    {
        _strategy = new DefaultStepCompletionStrategy(
            _prospectClient.Object,
            Mock.Of<ILogger<DefaultStepCompletionStrategy>>());
    }

    /// <summary>
    /// Verifies that blank step names are not handled by the default strategy.
    /// </summary>
    /// <param name="stepName">The step name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CanHandle_WhenStepNameIsBlank_ReturnsFalse(string? stepName)
    {
        _strategy.CanHandle(stepName!).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that non-blank step names are handled by the default strategy.
    /// </summary>
    [Fact]
    public void CanHandle_WhenStepNameIsProvided_ReturnsTrue()
    {
        _strategy.CanHandle("SupportingDocuments").Should().BeTrue();
    }

    /// <summary>
    /// Verifies that successful Prospect completion returns an empty upload batch.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WhenProspectCompletesStep_ReturnsEmptyUploadBatch()
    {
        _prospectClient
            .Setup(client => client.CompleteStepAsync(42, "SupportingDocuments", It.IsAny<CancellationToken>(), 7))
            .Returns(Task.CompletedTask);

        var result = await _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None, 7);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
        _prospectClient.Verify(
            client => client.CompleteStepAsync(42, "SupportingDocuments", It.IsAny<CancellationToken>(), 7),
            Times.Once);
    }

    /// <summary>
    /// Verifies that Prospect 404 responses are mapped to Gateway 404 errors.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WhenProspectReturnsNotFound_ThrowsGatewayNotFound()
    {
        _prospectClient
            .Setup(client => client.CompleteStepAsync(42, "Unknown", It.IsAny<CancellationToken>(), 7))
            .ThrowsAsync(new HttpRequestException("not found", null, HttpStatusCode.NotFound));

        Func<Task> act = () => _strategy.CompleteAsync(42, "Unknown", CancellationToken.None, 7);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Verifies that non-404 Prospect errors are not swallowed by the default strategy.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WhenProspectReturnsUnexpectedError_PropagatesHttpRequestException()
    {
        _prospectClient
            .Setup(client => client.CompleteStepAsync(42, "SupportingDocuments", It.IsAny<CancellationToken>(), 7))
            .ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));

        Func<Task> act = () => _strategy.CompleteAsync(42, "SupportingDocuments", CancellationToken.None, 7);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }
}
