using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.UnitTests.ProspectExperience.Models;

/// <summary>
/// Unit tests for <see cref="ProspectDocumentContentResponse"/>.
/// </summary>
public sealed class ProspectDocumentContentResponseTests
{
    /// <summary>
    /// Verifies that document content responses expose the downloaded content and metadata.
    /// </summary>
    [Fact]
    public void Constructor_WhenValuesAreProvided_ExposesAllValues()
    {
        byte[] content = [1, 2, 3];

        var response = new ProspectDocumentContentResponse(
            content,
            "application/pdf",
            "PASSEPORT_DUPONT_Jean");

        response.Content.Should().BeSameAs(content);
        response.ContentType.Should().Be("application/pdf");
        response.FileName.Should().Be("PASSEPORT_DUPONT_Jean");
    }
}
