using ApiGateway.Exceptions;
using Ocelot.DependencyInjection;

namespace ApiGateway.LocalRouting;

/// <summary>
/// Builds the Ocelot configuration of a local run: in-memory merge of the repository
/// ocelot.*.json files, token substitution, then downstream rewriting.
/// Only called in Development.
/// </summary>
public static class LocalRoutingExtensions
{
    public static IConfigurationBuilder AddLocalRouting(
        this IConfigurationBuilder builder,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = configuration.GetSection(LocalRoutingOptions.SectionName).Get<LocalRoutingOptions>()
            ?? throw new GatewayException(
                StatusCodes.Status500InternalServerError,
                Errors.NullConfigurationCode,
                string.Format(Errors.NullConfigurationMessage, LocalRoutingOptions.SectionName));

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            throw new GatewayException(
                StatusCodes.Status500InternalServerError,
                Errors.NullConfigurationCode,
                string.Format(Errors.NullConfigurationMessage, $"{LocalRoutingOptions.SectionName}:PublicBaseUrl"));
        }

        var folder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.ConfigFolder));

        // Merge into a separate builder: the result must be read to compute the overrides.
        var merged = new ConfigurationBuilder()
            .AddOcelot(folder, environment, MergeOcelotJson.ToMemory)
            .Build();

        var tokens = TokenSubstitution.BuildTokenOverrides(merged, options);
        var routes = RouteOverrideBuilder.BuildRouteOverrides(merged, options);

        foreach (var service in routes.UnknownServices)
        {
            // The logging pipeline is not built yet at this point of the startup.
            Console.WriteLine(
                $"[LocalRouting] Service '{service}' is declared in {LocalRoutingOptions.SectionName}:Services " +
                "but matches no route. Check the spelling of the segment.");
        }

        // Order drives precedence: the merge, then the tokens, then the routes.
        return builder
            .AddConfiguration(merged)
            .AddInMemoryCollection(tokens)
            .AddInMemoryCollection(routes.Overrides);
    }
}
