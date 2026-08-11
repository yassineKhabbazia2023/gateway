using ApiGateway.Configuration;
using ApiGateway.Identity.Models;

namespace ApiGateway.Identity.Repositories;

/// <summary>
/// Reads the JWT authorities from the configuration instead of an Azure Table.
/// Intended for a local run, where the real storage sits behind a private endpoint.
/// Every value read here resolves the #{gigya_api_key}# token against the GigyaApiKey entry of the
/// configuration: the api key of the site appears both in the fetch URL of the public JWK and in
/// the issuer of the customer tokens, and is only declared once, in appsettings.local.json.
/// </summary>
public sealed class ConfigurationAuthorityRepository(IConfiguration configuration) : IAuthorityRepository
{
    public const string SectionName = "Authorities";

    public Task<IReadOnlyList<AuthorityJson>> FindAuthorities(bool? enabled = true)
    {
        var gigyaApiKey = configuration[ConfigConstants.GigyaApiKeyConfigKey] ?? string.Empty;

        var authorities = configuration.GetSection(SectionName).GetChildren()
            .Select(section => Map(section, gigyaApiKey))
            .ToList();

        return Task.FromResult<IReadOnlyList<AuthorityJson>>(authorities);
    }

    private static AuthorityJson Map(IConfigurationSection section, string gigyaApiKey)
    {
        string Read(string? value) => (value ?? string.Empty)
            .Replace(ConfigConstants.GigyaApiKeyToken, gigyaApiKey, StringComparison.Ordinal);

        var audiences = section.GetSection("ValidAudiences").GetChildren()
            .Select(audience => new ValidAudience(Guid.NewGuid(), Read(audience.Value)))
            .ToList();

        var issuers = section.GetSection("ValidIssuers").GetChildren()
            .Select(issuer => new ValidIssuer(
                Guid.NewGuid(),
                Read(issuer["Name"]),
                Read(issuer["RoleName"]),
                bool.TryParse(issuer["CanBeSystemAccount"], out var system) && system))
            .ToList();

        var signingKeys = section.GetSection("SigningKeys").GetChildren()
            .Select(key => new SigningKey(Guid.NewGuid(), Read(key.Value)))
            .ToList();

        return new AuthorityJson(
            Guid.NewGuid(),
            Read(section["Name"]),
            Read(section["Url"]),
            bool.TryParse(section["ValidateIssuer"], out var validate) && validate,
            Read(section["JsonWebKeyFetchUrl"]),
            Read(section["NameClaimType"]),
            signingKeys,
            audiences,
            issuers);
    }
}
