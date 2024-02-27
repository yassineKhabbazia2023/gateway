using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ApiGateway.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Mocks;

[Route("api/ocelot-config")]
[ApiController]
[ExcludeFromCodeCoverage]
//TODO this is a temporary solution to provide some flexibility for frontend 
//TODO: Note it should not be delivered as it for Rec or prod Feature flag should excludes this  

public class OcelotConfigurationController(IConfiguration configuration) : ControllerBase
{
   

    [HttpGet]
    public async Task<IActionResult> GetOcelotConfig()
    {
        if (!System.IO.File.Exists(FileHelper.GetOcelotConfigFullPathName(configuration)))
        {
            return NotFound("Ocelot configuration file not found.");
        }

        var ocelotConfigJson = await System.IO.File.ReadAllTextAsync(FileHelper.GetOcelotConfigFullPathName(configuration));
        return Ok(ocelotConfigJson);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateOcelotConfig(dynamic updatedConfig)
    {
        string jsonContent = updatedConfig.ToString();
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            await System.IO.File.WriteAllTextAsync(FileHelper.GetOcelotConfigFullPathName(configuration), jsonContent);
            return Ok("Ocelot configuration updated successfully.");
        }
        catch (JsonException ex)
        {
            return BadRequest($"Invalid JSON format: {ex.Message}");
        }
    }


}