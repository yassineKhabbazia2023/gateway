using ApiGateway.Aggregator;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Ocelot.Middleware;
using Ocelot.Values;
using System.Net;

namespace ApiGateway.UnitTests.Aggregator
{
    public class WalletInfoAggregatorTests
    {
        private const string AccountRouteKey = "WalletInfoAccount";
        private const string ProspectRouteKey = "WalletInfoProspect";

        [Fact]
        public async Task Aggregate_BothResponsesOk_ReturnsMergedJson()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            Assert.Equal(24, response!["regularEntitiesCount"]);
            Assert.Equal(3, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_AccountForbidden_ReturnsForbidden()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ProspectForbidden_ReturnsForbidden()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_AccountResponseNull_ReturnsInternalServerError()
        {
            // Arrange
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext("UnknownRoute", BuildResponse(HttpStatusCode.OK, string.Empty));
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ProspectResponseNull_ReturnsInternalServerError()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext("UnknownRoute", BuildResponse(HttpStatusCode.OK, string.Empty));

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_EmptyPayloads_ReturnsZeroCounts()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, string.Empty);
            var prospectResponse = BuildResponse(HttpStatusCode.OK, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            Assert.Equal(0, response!["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_InvalidJson_ReturnsZeroCounts()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, "{invalid-json");
            var prospectResponse = BuildResponse(HttpStatusCode.OK, "{invalid-json");

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = new WalletInfoAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            Assert.Equal(0, response!["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        private static DefaultHttpContext BuildContext(string routeKey, DownstreamResponse response)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["DownstreamResponse"] = response;
            httpContext.Items["DownstreamRoute"] = BuildRoute(routeKey);
            return httpContext;
        }

        private static DownstreamRoute BuildRoute(string key)
        {
            return new DownstreamRoute(
                key: key,
                upstreamPathTemplate: new UpstreamPathTemplate("template", 1, true, ""),
                upstreamHeadersFindAndReplace: null,
                downstreamHeadersFindAndReplace: null,
                downstreamAddresses: null,
                serviceName: "serviceName",
                serviceNamespace: "serviceNamespace",
                httpHandlerOptions: null,
                qosOptions: null,
                downstreamScheme: "http",
                requestIdKey: null,
                cacheOptions: null,
                loadBalancerOptions: null,
                rateLimitOptions: null,
                routeClaimsRequirement: null,
                claimsToQueries: null,
                claimsToHeaders: null,
                claimsToClaims: null,
                claimsToPath: null,
                authenticationOptions: null,
                downstreamPathTemplate: null,
                loadBalancerKey: null,
                delegatingHandlers: null,
                addHeadersToDownstream: null,
                addHeadersToUpstream: null,
                dangerousAcceptAnyServerCertificateValidator: false,
                securityOptions: null,
                downstreamHttpMethod: null,
                downstreamHttpVersion: null,
                downstreamHttpVersionPolicy: HttpVersionPolicy.RequestVersionOrLower,
                upstreamHeaders: null,
                metadataOptions: new MetadataOptions(new Ocelot.Configuration.File.FileMetadataOptions()),
                timeout: null
            );
        }

        private static DownstreamResponse BuildResponse(HttpStatusCode statusCode, string content)
        {
            return new DownstreamResponse(
                new StringContent(content),
                statusCode,
                new List<Header>(),
                "reason");
        }
    }
}
