using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiGateway.Mocks.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Mocks;

[Route("api/mocks-configuration")]
[ApiController]
[ExcludeFromCodeCoverage]
//TODO this is a temporary solution to provide some flexibility for frontend 
public class MocksConfigurationController(IConfiguration configuration) : ControllerBase
{
    private string MockRepositoryPath => configuration["MOCK_REPOSITORY_PATH"]!;
    private string OcelotConfigPath => Path.Combine(configuration["OCELOT_CONFIG_PATH"]!, "ocelot.json");
    private string MockIndexPath => Path.Combine(MockRepositoryPath, "mock_index.json");

    [HttpGet("get-ocelot-config")]
    public async Task<IActionResult> GetOcelotConfig()
    {
        if (!System.IO.File.Exists(OcelotConfigPath))
        {
            return NotFound("Ocelot configuration file not found.");
        }

        var ocelotConfigJson = await System.IO.File.ReadAllTextAsync(OcelotConfigPath);
        return Ok(ocelotConfigJson);
    }

    [HttpPut("update-ocelot-config")]
    public async Task<IActionResult> UpdateOcelotConfig([FromBody] RouteConfig updatedConfig)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OcelotConfigPath)!);

        await System.IO.File.WriteAllTextAsync(OcelotConfigPath, JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions { WriteIndented = true }));
        return Ok("Ocelot configuration updated successfully.");
    }

    [HttpGet("get-mock-index")]
    public async Task<IActionResult> GetMockIndex()
    {
        if (!System.IO.File.Exists(MockIndexPath))
        {
            return NotFound("Mock index file not found.");
        }

        var mockIndexJson = await System.IO.File.ReadAllTextAsync(MockIndexPath);
        return Ok(mockIndexJson);
    }

    [HttpPut("update-mock-index")]
    public async Task<IActionResult> UpdateMockIndex([FromBody] List<MockIndex> updatedMockIndex)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MockIndexPath)!);

        var updatedMockIndexJson = JsonSerializer.Serialize(updatedMockIndex, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(MockIndexPath, updatedMockIndexJson);

        return Ok("Mock index updated successfully.");
    }
    
    [HttpPost("modify-mock-response"), DisableRequestSizeLimit]
    public async Task<IActionResult> ModifyMockResponse([FromForm] MockIndex update,MockEntryFileUpdate? entryFileUpdate )
    {
        if (string.IsNullOrWhiteSpace(update.ResponseMockJsonFile) || entryFileUpdate == null)
        {
            return BadRequest("Invalid mock response update provided.");
        }
        
        Directory.CreateDirectory(MockRepositoryPath);
        
        var mockResponseFilePath = Path.Combine(MockRepositoryPath, update.ResponseMockJsonFile);
        
        using (var stream = new FileStream(mockResponseFilePath, FileMode.Create))
        {
            await entryFileUpdate.File.CopyToAsync(stream);
        }
        
        var mockIndexes = new List<MockIndex>();
        if (System.IO.File.Exists(MockIndexPath))
        {
            var existingContent = await System.IO.File.ReadAllTextAsync(MockIndexPath);
            mockIndexes = JsonSerializer.Deserialize<List<MockIndex>>(existingContent) ?? mockIndexes;
        }
        
        var entryExists = mockIndexes.Any(mi => mi.ResponseMockJsonFile == update.ResponseMockJsonFile);
        if (entryExists) return Ok("Mock response updated successfully.");
        mockIndexes.Add(new MockIndex
        {
            ResponseMockJsonFile = update.ResponseMockJsonFile,
            DownstreamUri = update.DownstreamUri,
            HttpVerb = update.HttpVerb
        });
            
        await System.IO.File.WriteAllTextAsync(MockIndexPath, JsonSerializer.Serialize(mockIndexes, new JsonSerializerOptions { WriteIndented = true }));

        return Ok("Mock response updated successfully.");
    }


}