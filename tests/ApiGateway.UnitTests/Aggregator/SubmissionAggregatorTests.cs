using ApiGateway.Aggregator;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Ocelot.Middleware;
using Ocelot.Values;
using System.Net;
using System.Net.Http;

namespace ApiGateway.UnitTests.Aggregator
{
    public class SubmissionAggregatorTests
    {
        private const string RegistryRouteKey = "HubspotSubmissionRegistry";
        private const string AccountRouteKey = "VentyaDematReady";

        [Fact]
        public async Task Aggregate_ShouldReturnNotFound_WhenRegistryReturnsNotFound()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.NotFound, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = true }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            Assert.Equal(string.Empty, resultContent);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnCompleted_WhenRegistryOkAndAccountIsReadyTrue()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = "true" }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("completed", resultContent);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnSubmitted_WhenRegistryOkAndAccountIsReadyFalse()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = false }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("submitted", resultContent);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenRegistryOkAndAccountNotFound()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.NotFound, string.Empty);

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenAccountPayloadInvalid()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { invalid = true }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenRegistryRouteKeyMissing()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = true }));

            var httpContextA = BuildContext("UnknownRoute", responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnRegistryResponse_WhenRegistryStatusNotHandled()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.InternalServerError, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = true }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenAccountPayloadIsInvalidJson()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, "{invalid-json");

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenAccountPayloadIsNotObject()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, "[]");

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenAccountPayloadIsNotBoolean()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = "maybe" }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
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
                useServiceDiscovery: false,
                enableEndpointEndpointRateLimiting: false,
                qosOptions: null,
                downstreamScheme: "http",
                requestIdKey: null,
                isCached: false,
                cacheOptions: null,
                loadBalancerOptions: null,
                rateLimitOptions: null,
                routeClaimsRequirement: null,
                claimsToQueries: null,
                claimsToHeaders: null,
                claimsToClaims: null,
                claimsToPath: null,
                isAuthenticated: false,
                isAuthorized: false,
                authenticationOptions: null,
                downstreamPathTemplate: null,
                loadBalancerKey: null,
                delegatingHandlers: null,
                addHeadersToDownstream: null,
                addHeadersToUpstream: null,
                dangerousAcceptAnyServerCertificateValidator: false,
                securityOptions: null,
                downstreamHttpMethod: null,
                downstreamHttpVersion: null
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
