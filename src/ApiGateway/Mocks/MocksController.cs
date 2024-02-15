using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Mocks.Models;
using LiteDB;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Mocks;

[ApiController]
[Route("mocks")]
public class MocksController : ControllerBase
{
    private readonly ILiteDatabase _database;

    public MocksController(ILiteDatabase database)
    {
        _database = database;
    }

    [HttpGet("get-mock-index")]
    public IActionResult GetMockIndex()
    {
        var mockIndexCollection = _database.GetCollection<MockIndexDoc>(MocksConstants.MockResponsesCollection);
        var mockIndexes = mockIndexCollection.FindAll();

        return Ok(mockIndexes);
    }
    
    /// <summary>
    /// It will update or insert a new response mock json response
    /// </summary>
    /// <param name="request"></param>
    /// <param name="fileUpdate"></param>
    /// <returns></returns>
    [HttpPost("modify-mock-response")]
    public async Task<IActionResult> ModifyMockResponse([FromForm] MockIndexRequest? request, [FromForm] MockEntryFileUpdate? fileUpdate)
    {
        if (fileUpdate?.File == null || request == null)
        {
            return BadRequest("Invalid mock response update provided.");
        }
    
        if (!fileUpdate.File.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only JSON files are accepted.");
        }
        string fileContent;
        using (var streamReader = new StreamReader(fileUpdate.File.OpenReadStream()))
        {
            fileContent = await streamReader.ReadToEndAsync();
        }
        try
        {
            var jsonCheck = System.Text.Json.JsonSerializer.Deserialize<object>(fileContent);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("The file content is not valid JSON.");
        }
        var mockIndexDoc = new MockIndexDoc
        {
            DownstreamUri = request.DownstreamUri,
            HttpVerb = request.HttpVerb,
            JsonContent = fileContent, 
        };
        mockIndexDoc.Id = mockIndexDoc.GenerateId(); 
    
        var mockIndexCollection = _database.GetCollection<MockIndexDoc>(MocksConstants.MockResponsesCollection);
        var existingDoc = mockIndexCollection.FindById(mockIndexDoc.Id);
        if (existingDoc != null)
        {
            mockIndexCollection.Update(mockIndexDoc);
        }
        else
        {
            mockIndexCollection.Insert(mockIndexDoc);
        }

        return Ok("Mock response updated successfully.");
    }


}