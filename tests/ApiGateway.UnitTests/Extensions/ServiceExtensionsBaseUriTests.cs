using ApiGateway.Exceptions;
using ApiGateway.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.UnitTests.Extensions;

/// <summary>
/// Guards the downstream base address composition.
///
/// The services address their downstream with a relative URI (api/subscription, not
/// /api/subscription): a root relative path would drop the prefix carried by the base
/// address behind the public entry point. <see cref="ServiceExtensions.GetBaseUri"/> is the
/// other half of that contract: the trailing slash without which the framework drops the
/// last segment of the base path, and the normalisation down to the root of the service, so
/// that a configured value still carrying an /api suffix does not produce /api/api.
/// </summary>
public class ServiceExtensionsBaseUriTests
{
    private const string Key = "OfferApiUri";

    private static IConfiguration ConfigurationWith(string? value)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = value })
            .Build();
    }

    [Theory]
    // A base address without a path is every deployed environment: only the slash is added.
    [InlineData("https://appcegpulseoff.example.net", "https://appcegpulseoff.example.net/")]
    [InlineData("https://appcegpulseoff.example.net/", "https://appcegpulseoff.example.net/")]
    // Behind the public entry point the base address carries the service prefix.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer", "https://api-itg01.itg.pulse.rydge.fr/offer/")]
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer/", "https://api-itg01.itg.pulse.rydge.fr/offer/")]
    // A host merely containing "api" is not a path segment: nothing to strip here.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr", "https://api-itg01.itg.pulse.rydge.fr/")]
    public void GetBaseUri_ShouldGuaranteeATrailingSlash(string configured, string expected)
    {
        var actual = ServiceExtensions.GetBaseUri(ConfigurationWith(configured), Key);

        actual.ToString().Should().Be(expected);
    }

    /// <summary>
    /// Until the clients addressed their downstream with a root relative path, the /api suffix
    /// carried by some configured values was dead weight: HttpClient discarded the whole base
    /// path. Now that every client path is relative and starts with api/, that suffix would be
    /// duplicated into /api/api. The base address is therefore normalised down to the root of
    /// the service, which keeps the deployed values that still carry the suffix working.
    /// </summary>
    [Theory]
    [InlineData("https://appcegpulseoff.example.net/api", "https://appcegpulseoff.example.net/")]
    [InlineData("https://appcegpulseoff.example.net/api/", "https://appcegpulseoff.example.net/")]
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer/api", "https://api-itg01.itg.pulse.rydge.fr/offer/")]
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/contact/api/", "https://api-itg01.itg.pulse.rydge.fr/contact/")]
    // Only a trailing segment is a suffix: an /api in the middle belongs to the prefix.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/api/offer", "https://api-itg01.itg.pulse.rydge.fr/api/offer/")]
    public void GetBaseUri_WhenTheConfiguredValueCarriesTheApiSuffix_ShouldNormaliseItAway(string configured, string expected)
    {
        var actual = ServiceExtensions.GetBaseUri(ConfigurationWith(configured), Key);

        actual.ToString().Should().Be(expected);
    }

    /// <summary>
    /// The status belongs to the contract here, unlike the configuration guards of the startup.
    /// The AddHttpClient configure delegates only run when IHttpClientFactory builds the client,
    /// so this throw surfaces on the first request that resolves the service and its status is
    /// serialized to the caller. A missing key is a server misconfiguration: 500, not a 4xx.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetBaseUri_WhenTheConfigurationIsMissing_ShouldThrow(string? configured)
    {
        var act = () => ServiceExtensions.GetBaseUri(ConfigurationWith(configured), Key);

        var exception = act.Should().Throw<GatewayException>().Which;

        exception.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        exception.ErrorCode.Should().Be(Errors.NullConfigurationCode);
        exception.Message.Should().Contain(Key);
    }

    /// Goes through a real HttpClient so that the base-address combination under test is the
    /// one the framework performs, not one reimplemented by the test. HttpMessageInvoker does
    /// not apply BaseAddress and cannot cover this case.
    [Theory]
    [InlineData("https://appcegpulseoff.example.net", "api/subscription", "https://appcegpulseoff.example.net/api/subscription")]
    // Without the trailing slash added by GetBaseUri, /offer would be silently lost here.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer", "api/subscription", "https://api-itg01.itg.pulse.rydge.fr/offer/api/subscription")]
    // The query string must survive the composition.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer", "api/subscription/status?accountId=602", "https://api-itg01.itg.pulse.rydge.fr/offer/api/subscription/status?accountId=602")]
    // Contact is configured with an /api suffix: normalising the base and prefixing the client
    // path lands on the very same URL, which is what makes the change safe for that service.
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/contact/api", "api/contacts?Email=a%40b.fr", "https://api-itg01.itg.pulse.rydge.fr/contact/api/contacts?Email=a%40b.fr")]
    // The offending case: an /api suffix would double up without the normalisation.
    [InlineData("https://appcegpulseoff.example.net/api", "api/subscription", "https://appcegpulseoff.example.net/api/subscription")]
    [InlineData("https://api-itg01.itg.pulse.rydge.fr/offer/api", "api/subscription/status?accountId=602", "https://api-itg01.itg.pulse.rydge.fr/offer/api/subscription/status?accountId=602")]
    public async Task GetBaseUri_CombinedWithARelativeRequest_ShouldPreserveThePathPrefix(
        string configured,
        string requestUri,
        string expected)
    {
        var innerHandler = new CapturingHandler();
        using var client = new HttpClient(innerHandler)
        {
            BaseAddress = ServiceExtensions.GetBaseUri(ConfigurationWith(configured), Key)
        };

        await client.GetAsync(requestUri);

        innerHandler.LastRequestUri.Should().NotBeNull();
        innerHandler.LastRequestUri!.ToString().Should().Be(expected);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
