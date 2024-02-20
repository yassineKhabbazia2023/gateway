using System.Diagnostics.CodeAnalysis;
using LiteDB;

namespace ApiGateway.Mocks.Models;

[ExcludeFromCodeCoverage]
public class MockIndexDoc
{

    [BsonId] public string Id { get; set; } = null!;
    public required string DownstreamUri { get; set; }
    public required string HttpVerb { get; set; }
    public required string JsonContent { get; set; }

    public string GenerateId()
    {
        return $"{HttpVerb}:{DownstreamUri}".ToLower();
    }
}

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