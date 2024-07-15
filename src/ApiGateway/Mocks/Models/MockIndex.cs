using System.Diagnostics.CodeAnalysis;

namespace ApiGateway.Mocks.Models;

[ExcludeFromCodeCoverage]
public class MockEntryFileUpdate
{
    public IFormFile? File { get; set; }
}

[ExcludeFromCodeCoverage]
public class MockIndexRequest
{
    public required string DownstreamUri { get; set; }
    public required string HttpVerb { get; set; }
}