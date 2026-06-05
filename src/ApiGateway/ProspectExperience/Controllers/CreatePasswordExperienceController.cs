using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Mvc;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.ProspectExperience.Controllers;

/// <summary>
/// Exposes the Prospect create-password experience endpoint.
/// </summary>
[Route("gtw/password-experience/api")]
[ApiController]
public class CreatePasswordExperienceController(ICreatePasswordExperienceService createPasswordExperienceService) : ControllerBase
{
    /// <summary>
    /// Creates a new password using the default Contact flow or the Prospect template flow.
    /// </summary>
    /// <param name="createPasswordExperienceRequest">The Gateway create-password experience request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Contact downstream response.</returns>
    [HttpPost("createNewPassword")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateNewPassword(
        [FromBody] CreatePasswordExperienceRequest createPasswordExperienceRequest,
        CancellationToken ct = default)
    {
        using var response = await createPasswordExperienceService.CreateNewPasswordAsync(createPasswordExperienceRequest, ct);
        return await ToActionResultAsync(response, ct);
    }

    /// <summary>
    /// Converts a downstream HTTP response into an MVC result while preserving the status and response body.
    /// </summary>
    /// <param name="response">The downstream response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An action result matching the downstream response.</returns>
    private static async Task<IActionResult> ToActionResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        if (response.Content is null)
        {
            return new StatusCodeResult(statusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(content))
        {
            return new StatusCodeResult(statusCode);
        }

        return new ContentResult
        {
            StatusCode = statusCode,
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString()
        };
    }
}
