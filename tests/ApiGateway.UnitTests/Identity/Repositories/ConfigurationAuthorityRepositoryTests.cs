using ApiGateway.Identity.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.UnitTests.Identity.Repositories;

public class ConfigurationAuthorityRepositoryTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorities:0:Name"] = "AAD",
            ["Authorities:0:Url"] = "https://login.microsoftonline.com/tenant",
            ["Authorities:0:ValidateIssuer"] = "true",
            ["Authorities:0:JsonWebKeyFetchUrl"] = "",
            ["Authorities:0:NameClaimType"] = "email",
            ["Authorities:0:ValidAudiences:0"] = "api://pulse",
            ["Authorities:0:ValidIssuers:0:Name"] = "https://sts.windows.net/tenant/",
            ["Authorities:0:ValidIssuers:0:RoleName"] = "Collaborator",
            ["Authorities:0:ValidIssuers:0:CanBeSystemAccount"] = "true",
        })
        .Build();

    [Fact]
    public async Task FindAuthorities_ShouldReadTheAuthoritiesFromTheConfiguration()
    {
        var repository = new ConfigurationAuthorityRepository(Config());

        var authorities = await repository.FindAuthorities();

        authorities.Should().HaveCount(1);
        authorities[0].Name.Should().Be("AAD");
        authorities[0].NameClaimType.Should().Be("email");
        authorities[0].ValidateIssuer.Should().BeTrue();
        authorities[0].ValidAudiences.Should().ContainSingle(audience => audience.Name == "api://pulse");
        authorities[0].ValidIssuers.Should().ContainSingle(issuer => issuer.RoleName == "Collaborator");
        authorities[0].ValidIssuers[0].CanBeSystemAccount.Should().BeTrue();
        authorities[0].SigningKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAuthorities_WhenTheSectionIsMissing_ShouldReturnAnEmptyList()
    {
        var repository = new ConfigurationAuthorityRepository(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

        var authorities = await repository.FindAuthorities();

        authorities.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAuthorities_ShouldReadTheSigningKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorities:0:Name"] = "Local",
                ["Authorities:0:SigningKeys:0"] = "symmetric-key",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities[0].SigningKeys.Should().ContainSingle(key => key.JsonWebKey == "symmetric-key");
    }

    [Fact]
    public async Task FindAuthorities_WhenTheValuesAreMissing_ShouldReplaceThemWithEmptyStrings()
    {
        // The sections only have children: their own Value is null. This is the case of an
        // incomplete configuration, which must not make the read fail at startup.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorities:0:ValidAudiences:0:Unexpected"] = "value",
                ["Authorities:0:ValidIssuers:0:Unexpected"] = "value",
                ["Authorities:0:SigningKeys:0:Unexpected"] = "value",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities.Should().HaveCount(1);
        authorities[0].Name.Should().BeEmpty();
        authorities[0].Url.Should().BeEmpty();
        authorities[0].JsonWebKeyFetchUrl.Should().BeEmpty();
        authorities[0].NameClaimType.Should().BeEmpty();
        authorities[0].ValidateIssuer.Should().BeFalse();
        authorities[0].ValidAudiences.Should().ContainSingle(audience => audience.Name == string.Empty);
        authorities[0].SigningKeys.Should().ContainSingle(key => key.JsonWebKey == string.Empty);
        authorities[0].ValidIssuers.Should().ContainSingle();
        authorities[0].ValidIssuers[0].Name.Should().BeEmpty();
        authorities[0].ValidIssuers[0].RoleName.Should().BeEmpty();
        authorities[0].ValidIssuers[0].CanBeSystemAccount.Should().BeFalse();
    }

    /// <summary>
    /// The Gigya authorities carry no signing key: they are validated against the public JWK
    /// published by accounts.getJWTPublicKey, fetched at startup from this URL. Losing the
    /// mapping of this single field is enough to make every customer token fail locally.
    /// </summary>
    [Fact]
    public async Task FindAuthorities_ShouldReadTheJsonWebKeyFetchUrl()
    {
        const string fetchUrl = "https://accounts.eu1.gigya.com/accounts.getJWTPublicKey?apiKey=3_key";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorities:0:Name"] = "GIGYA MyPulse v2",
                ["Authorities:0:NameClaimType"] = "username",
                ["Authorities:0:JsonWebKeyFetchUrl"] = fetchUrl,
                ["Authorities:0:ValidIssuers:0:Name"] = "https://fidm.gigya.com/jwt/3_key/",
                ["Authorities:0:ValidIssuers:0:RoleName"] = "Customer",
                ["Authorities:0:ValidIssuers:0:CanBeSystemAccount"] = "false",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities[0].JsonWebKeyFetchUrl.Should().Be(fetchUrl);
        authorities[0].NameClaimType.Should().Be("username");
        authorities[0].SigningKeys.Should().BeEmpty();
        authorities[0].ValidIssuers.Should().ContainSingle(issuer => issuer.RoleName == "Customer");
        authorities[0].ValidIssuers[0].CanBeSystemAccount.Should().BeFalse();
    }

    /// <summary>
    /// The api key of the Gigya site is declared once, in appsettings.local.json, and referenced
    /// through the #{gigya_api_key}# token by the fetch URL of the JWK and by the issuer. An
    /// unresolved token silently breaks every customer token, the issuer being compared by equality.
    /// </summary>
    [Fact]
    public async Task FindAuthorities_ShouldResolveTheGigyaApiKeyToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GigyaApiKey"] = "4_site-key",
                ["Authorities:0:Name"] = "GIGYA MyPulse v2",
                ["Authorities:0:JsonWebKeyFetchUrl"] =
                    "https://accounts.eu1.gigya.com/accounts.getJWTPublicKey?apiKey=#{gigya_api_key}#",
                ["Authorities:0:ValidIssuers:0:Name"] = "https://fidm.gigya.com/jwt/#{gigya_api_key}#/",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities[0].JsonWebKeyFetchUrl.Should()
            .Be("https://accounts.eu1.gigya.com/accounts.getJWTPublicKey?apiKey=4_site-key");
        authorities[0].ValidIssuers[0].Name.Should().Be("https://fidm.gigya.com/jwt/4_site-key/");
    }

    [Fact]
    public async Task FindAuthorities_WhenTheGigyaApiKeyIsMissing_ShouldLeaveTheValuesWithoutTheToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorities:0:ValidIssuers:0:Name"] = "https://fidm.gigya.com/jwt/#{gigya_api_key}#/",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities[0].ValidIssuers[0].Name.Should().Be("https://fidm.gigya.com/jwt//");
    }

    [Fact]
    public async Task FindAuthorities_WhenValidateIssuerIsFalse_ShouldNotValidateTheIssuer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorities:0:Name"] = "Local",
                ["Authorities:0:ValidateIssuer"] = "false",
            })
            .Build();

        var authorities = await new ConfigurationAuthorityRepository(configuration).FindAuthorities();

        authorities[0].ValidateIssuer.Should().BeFalse();
    }
}
