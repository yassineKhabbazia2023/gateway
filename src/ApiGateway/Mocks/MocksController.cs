using ApiGateway.DelegatingHandlers.Mocks;
using ApiGateway.Mocks.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using System.Net;

namespace ApiGateway.Mocks;

[ApiController]
[Route("mocks")]
public class MocksController : ControllerBase
{
    private readonly IMockResponseRepository repository;
    private readonly IFeatureManager featureManager;

    public MocksController(IMockResponseRepository repository, IFeatureManager featureManager)
    {
        this.repository = repository;
        this.featureManager = featureManager;
    }

    [HttpGet("get-mock-index")]
    public async Task<IActionResult> GetMockIndex()
    {
        if (!await featureManager.IsEnabledAsync("Mocks"))
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        var mockEntries = await repository.ListAllAsync();
        return Ok(mockEntries);
    }

    /// <summary>
    /// It will update or insert a new response mock json response
    /// </summary>
    [HttpPost("modify-mock-response")]
    public async Task<IActionResult> ModifyMockResponse([FromForm] MockIndexRequest? request,
        [FromForm] MockEntryFileUpdate? fileUpdate)
    {
        if (!await featureManager.IsEnabledAsync("Mocks"))
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        if (fileUpdate?.File == null || request == null)
        {
            return BadRequest("Invalid mock response update provided.");
        }

        if (!fileUpdate.File.FileName.EndsWith(".json",
                StringComparison.OrdinalIgnoreCase))
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
            System.Text.Json.JsonSerializer.Deserialize<object>(fileContent);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("The file content is not valid JSON.");
        }

        var routeKey = $"{request.HttpVerb}:{request.DownstreamUri}".ToLower();
        await repository.UpsertAsync(routeKey, fileContent);

        return Ok("Mock response updated successfully.");
    }

    [HttpDelete("delete-mock-response")]
    public async Task<IActionResult> DeleteMockResponse([FromQuery] string routeKey)
    {
        if (!await featureManager.IsEnabledAsync("Mocks"))
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(routeKey))
        {
            return BadRequest("Route key is required.");
        }

        var deleted = await repository.DeleteAsync(routeKey);
        if (!deleted)
        {
            return NotFound("Mock response not found.");
        }

        return Ok("Mock response deleted successfully.");
    }
}
