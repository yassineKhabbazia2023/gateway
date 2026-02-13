using ApiGateway.Aggregator;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Ocelot.Middleware;
using Ocelot.Values;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;

namespace ApiGateway.UnitTests.Aggregator
{
    public class SubmissionAggregatorTests
    {
        private const string RegistryRouteKey = "HubspotSubmissionRegistry";
        private const string AccountRouteKey = "VentyaDematReady";
        private const string TestUserEmail = "user@example.com";

        [Fact]
        public async Task Aggregate_ShouldReturnConnectionReadyUnlocked_WhenIsReadyTrueAndEmailMatches()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = true,
                contactWithAccess = TestUserEmail,
                externalDematMail = "external@example.com"
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("connection-ready-unlocked", response!["tab-state"]);
            Assert.Equal(TestUserEmail, response["contactWithAccess"]);
            Assert.Equal("external@example.com", response["externalDematMail"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnConnectionReadyLocked_WhenIsReadyTrueAndEmailDiffers()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = true,
                contactWithAccess = "other@example.com",
                externalDematMail = "external@example.com"
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("connection-ready-locked", response!["tab-state"]);
            Assert.Equal("other@example.com", response["contactWithAccess"]);
            Assert.Equal("external@example.com", response["externalDematMail"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnFormStart_WhenIsReadyFalseAndRegistryReturnsNotFound()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.NotFound, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = false,
                contactWithAccess = "toto@gmail.com",
                externalDematMail = "titi@gmail.com"
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("form-start", response!["tab-state"]);
            Assert.Equal("toto@gmail.com", response["contactWithAccess"]);
            Assert.Equal("titi@gmail.com", response["externalDematMail"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnFormSubmitted_WhenIsReadyFalseAndRegistryReturnsOk()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = false,
                contactWithAccess = "toto@gmail.com",
                externalDematMail = (string?)null
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("form-submitted", response!["tab-state"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnNotFound_WhenAccountReturnsNotFound()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.NotFound, string.Empty);

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnFormStart_WhenAccountReturnsErrorAndRegistryReturnsNotFound()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.NotFound, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.InternalServerError, string.Empty);

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("form-start", response!["tab-state"]);
            Assert.Null(response["contactWithAccess"]);
            Assert.Null(response["externalDematMail"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnFormSubmitted_WhenAccountReturnsErrorAndRegistryReturnsOk()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.InternalServerError, string.Empty);

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("form-submitted", response!["tab-state"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnInternalServerError_WhenRouteKeyMissing()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new { isReady = true }));

            var httpContextA = BuildContext("UnknownRoute", responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnConnectionReadyUnlocked_WhenEmailMatchesCaseInsensitive()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = true,
                contactWithAccess = "USER@EXAMPLE.COM",
                externalDematMail = "external@example.com"
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("connection-ready-unlocked", response!["tab-state"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnFormSubmitted_WhenAccountPayloadIsInvalidJson()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, "{invalid-json");

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("form-submitted", response!["tab-state"]);
        }

        [Fact]
        public async Task Aggregate_ShouldReturnNullMails_WhenAccountPayloadHasNoMailFields()
        {
            // Arrange
            var responseA = BuildResponse(HttpStatusCode.OK, string.Empty);
            var responseB = BuildResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(new
            {
                isReady = true
            }));

            var httpContextA = BuildContext(RegistryRouteKey, responseA, TestUserEmail);
            var httpContextB = BuildContext(AccountRouteKey, responseB);

            var aggregator = new SubmissionAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextA, httpContextB });

            // Assert
            var json = await result.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);
            Assert.Equal("connection-ready-locked", response!["tab-state"]);
            Assert.Null(response["contactWithAccess"]);
            Assert.Null(response["externalDematMail"]);
        }

        private static DefaultHttpContext BuildContext(string routeKey, DownstreamResponse response, string? userEmail = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["DownstreamResponse"] = response;
            httpContext.Items["DownstreamRoute"] = BuildRoute(routeKey);

            if (userEmail != null)
            {
                var token = GenerateJwtToken(userEmail);
                httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
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

        private static string GenerateJwtToken(string email)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_32_bytes_long!"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("email", email)
                }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = credentials
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(tokenDescriptor));
        }
    }
}
