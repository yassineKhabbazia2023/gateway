using ApiGateway.Aggregator;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Ocelot.Middleware;
using Ocelot.Values;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Aggregator
{
    public class WalletInfoAggregatorTests
    {
        private const string AccountRouteKey = "WalletInfoAccount";
        private const string ProspectRouteKey = "WalletInfoProspect";

        private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();

        private WalletInfoAggregator CreateAggregator(bool isProspectEnabled = true)
        {
            _featureFlagServiceMock
                .Setup(s => s.IsEnabledAsync(
                    FeatureFlagKeys.IsProspectExperienceEnabled,
                    It.IsAny<bool>(),
                    It.IsAny<FeatureContext?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(isProspectEnabled);

            return new WalletInfoAggregator(_featureFlagServiceMock.Object, NullLogger<WalletInfoAggregator>.Instance);
        }

        [Fact]
        public async Task Aggregate_ProspectEnabled_BothResponsesOk_ReturnsMergedJson()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator(isProspectEnabled: true);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(24, response["regularEntitiesCount"]);
            Assert.Equal(3, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_ProspectDisabled_ProspectForbidden_ReturnsOkWithoutProspectCount()
        {
            // Arrange - c'est exactement ce que renvoie ProspectExperienceHandler quand le flag est coupé
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator(isProspectEnabled: false);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(24, response["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_ProspectDisabled_ResponseContractStaysCompatible()
        {
            // Arrange - le front continue de lire les deux propriétés, elles doivent rester présentes
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator(isProspectEnabled: false);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var response = await ReadCountsAsync(result);
            Assert.Equal(2, response.Count);
            Assert.True(response.ContainsKey("regularEntitiesCount"));
            Assert.True(response.ContainsKey("prospectEntitiesCount"));
            Assert.Equal("application/json", result.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Aggregate_ProspectDisabled_ProspectResponseMissing_ReturnsOkWithoutProspectCount()
        {
            // Arrange - la route WalletInfoProspect a été supprimée (décommissionnement)
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);

            var aggregator = CreateAggregator(isProspectEnabled: false);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(24, response["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_ProspectEnabled_ProspectResponseMissing_ReturnsOkWithoutProspectCount()
        {
            // Arrange - la route a disparu alors que le flag est resté actif : l'endpoint doit tenir quand même
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);

            var aggregator = CreateAggregator(isProspectEnabled: true);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(24, response["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_ProspectDisabled_ProspectReturnsCount_IgnoresProspectCount()
        {
            // Arrange - flag coupé : on n'expose aucune donnée Prospect, même si la réponse en contient
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 7 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator(isProspectEnabled: false);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_AccountForbidden_ReturnsForbidden()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ProspectEnabled_ProspectForbidden_ReturnsForbidden()
        {
            // Arrange - flag actif : un 403 Prospect est un vrai refus d'autorisation, on ne le masque pas
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.Forbidden, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator(isProspectEnabled: true);

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_AccountResponseNull_ReturnsInternalServerErrorWithoutEvaluatingFlag()
        {
            // Arrange
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext("UnknownRoute", BuildResponse(HttpStatusCode.OK, string.Empty));
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
            _featureFlagServiceMock.Verify(
                s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Aggregate_EmptyPayloads_ReturnsZeroCounts()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, string.Empty);
            var prospectResponse = BuildResponse(HttpStatusCode.OK, string.Empty);

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(0, response["regularEntitiesCount"]);
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

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(0, response["regularEntitiesCount"]);
            Assert.Equal(0, response["prospectEntitiesCount"]);
        }

        [Fact]
        public async Task Aggregate_EvaluatesProspectFeatureFlagOnceWithTrueAsDefaultValue()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var prospectResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { prospectEntitiesCount = 3 }));

            var httpContextA = BuildContext(AccountRouteKey, accountResponse);
            var httpContextB = BuildContext(ProspectRouteKey, prospectResponse);

            var aggregator = CreateAggregator();

            // Act
            await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert - un défaut à true préserve le comportement actuel si le provider est injoignable
            _featureFlagServiceMock.Verify(
                s => s.IsEnabledAsync(
                    FeatureFlagKeys.IsProspectExperienceEnabled,
                    true,
                    It.IsAny<FeatureContext?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Aggregate_UsesJwtEmailAsFeatureFlagTargetingContext()
        {
            // Arrange - même source de targeting que ProspectExperienceHandler
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var httpContextA = BuildContext(AccountRouteKey, accountResponse, bearerToken: BuildJwt("user@test.fr"));

            FeatureContext? capturedContext = null;
            _featureFlagServiceMock
                .Setup(s => s.IsEnabledAsync(
                    FeatureFlagKeys.IsProspectExperienceEnabled,
                    It.IsAny<bool>(),
                    It.IsAny<FeatureContext?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, bool, FeatureContext?, CancellationToken>((_, _, context, _) => capturedContext = context)
                .ReturnsAsync(true);

            var aggregator = new WalletInfoAggregator(_featureFlagServiceMock.Object, NullLogger<WalletInfoAggregator>.Instance);

            // Act
            await aggregator.Aggregate(new List<HttpContext> { httpContextA });

            // Assert
            Assert.NotNull(capturedContext);
            Assert.Equal("user@test.fr", capturedContext!.Email);
        }

        [Fact]
        public async Task Aggregate_WithoutBearerToken_EvaluatesFlagWithoutTargetingContext()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var httpContextA = BuildContext(AccountRouteKey, accountResponse);

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            _featureFlagServiceMock.Verify(
                s => s.IsEnabledAsync(FeatureFlagKeys.IsProspectExperienceEnabled, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Aggregate_WithMalformedBearerToken_StillReturnsOk()
        {
            // Arrange
            var accountResponse = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { regularEntitiesCount = 24 }));
            var httpContextA = BuildContext(AccountRouteKey, accountResponse, bearerToken: "not-a-jwt");

            var aggregator = CreateAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var response = await ReadCountsAsync(result);
            Assert.Equal(24, response["regularEntitiesCount"]);
        }

        private static async Task<Dictionary<string, int>> ReadCountsAsync(DownstreamResponse response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(json)!;
        }

        private static string BuildJwt(string email)
        {
            var token = new JwtSecurityToken(claims: new[] { new Claim("upn", email) });
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static DefaultHttpContext BuildContext(string routeKey, DownstreamResponse response, string? bearerToken = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["DownstreamResponse"] = response;
            httpContext.Items["DownstreamRoute"] = BuildRoute(routeKey);

            if (!string.IsNullOrEmpty(bearerToken))
            {
                httpContext.Request.Headers["Authorization"] = $"Bearer {bearerToken}";
            }

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
