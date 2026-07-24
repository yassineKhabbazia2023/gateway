using System.Text.Json;

namespace ApiGateway.ProspectExperience.Helpers;

/// <summary>
/// Provides the shared JSON serialization options used by Prospect Experience downstream clients.
/// </summary>
internal static class ProspectExperienceJsonOptions
{
    /// <summary>
    /// Gets the camel-case, case-insensitive JSON options used for downstream API contracts.
    /// </summary>
    internal static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
