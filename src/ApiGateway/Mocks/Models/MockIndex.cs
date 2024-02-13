namespace ApiGateway.Mocks.Models;

public class MockIndex
{
    public required string DownstreamUri { get; set; }
    public required string HttpVerb { get; set; }
    public  required string ResponseMockJsonFile { get; set; }
}
public class MockEntryFileUpdate
{
    public IFormFile? File { get; set; }
}


