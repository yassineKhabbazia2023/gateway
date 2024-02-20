using ApiGateway.Mocks.Models;
using LiteDB;

namespace ApiGateway.DelegatingHandlers.Mocks;

public class MockResponseRepository(
    ILiteDatabase liteDatabase) : IMockResponseRepository
{
    private ILiteCollection<MockIndexDoc> _mockResponses =
        liteDatabase.GetCollection<MockIndexDoc>(MocksConstants.MockResponsesCollection);

    public (bool, string? JsonContent) GetJsonContent(string routeKey)
    {
        var mockResponse = _mockResponses.FindOne(x => x.Id == routeKey);
        return mockResponse != null
            ? (true, mockResponse.JsonContent)
            : (false, null)!;
    }
}