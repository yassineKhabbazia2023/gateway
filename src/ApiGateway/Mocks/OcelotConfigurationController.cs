using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using ApiGateway.FeatureFlags;
using ApiGateway.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Mocks;

[Route("api/ocelot-config")]
[ApiController]
[ExcludeFromCodeCoverage]
public class OcelotConfigurationController(IConfiguration configuration, IFeatureFlagService featureFlagService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOcelotConfig()
    {
        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsOcelotConfigEnabled))
        {
            return this.StatusCode((int)HttpStatusCode.Forbidden);
        }

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
        if (!await featureFlagService.IsEnabledAsync(FeatureFlagKeys.IsOcelotConfigEnabled))
        {
            return this.StatusCode((int)HttpStatusCode.Forbidden);
        }

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
